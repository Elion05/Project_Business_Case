using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BestelApp_Cons.Models;
using Microsoft.Extensions.Configuration;

namespace BestelApp_Cons.Salesforce
{
    /// <summary>
    /// Client om data naar Salesforce te sturen
    /// Gebruikt de SalesforceAuthService voor authentication
    /// </summary>
    public class SalesforceClient
    {
        private readonly IConfiguration _configuratie;
        private readonly SalesforceAuthService _authService;
        private readonly HttpClient _httpClient;

        public SalesforceClient(IConfiguration configuratie, SalesforceAuthService authService)
        {
            _configuratie = configuratie;
            _authService = authService;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Stuur een bestelling naar Salesforce
        /// Gebruikt UPSERT (update als bestaat, anders insert)
        /// </summary>
        /// <returns>True als succesvol, anders False</returns>
        public async Task<SalesforceResultaat> StuurBestellingAsync(OrderMessage bestelling)
        {
            try
            {
                // Probeer de bestelling te versturen
                return await VerstuurNaarSalesforceAsync(bestelling, eerstePoging: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Onverwachte fout bij Salesforce versturen: {ex.Message}");
                return new SalesforceResultaat
                {
                    IsSuccesvol = false,
                    StatusCode = 0,
                    IsHerhaalbaar = false,
                    Foutmelding = ex.Message
                };
            }
        }

        /// <summary>
        /// Stuur een fallback bericht naar Salesforce (als JSON parsing faalt)
        /// Maakt nu een Order aan in plaats van Lead
        /// </summary>
        public async Task<SalesforceResultaat> StuurFallbackBerichtAsync(string berichtTekst)
        {
            try
            {
                // Haal een geldig token op
                var accessToken = await _authService.HaalAccessTokenOpAsync();
                var instanceUrl = _configuratie["Salesforce:InstanceUrl"];
                var apiVersion = _configuratie["Salesforce:ApiVersion"] ?? "v60.0";

                // Maak een unieke Order ID op basis van timestamp
                var fallbackOrderId = $"FALLBACK-{DateTime.UtcNow:yyyyMMddHHmmss}";
                var accountId = _configuratie["Salesforce:DefaultAccountId"];

                // Maak JSON body voor Salesforce Order
                var orderData = new Dictionary<string, object>
                {
                    { "OrderNumber", fallbackOrderId },
                    { "Status", "Draft" },
                    { "TotalAmount", 0 },
                    { "EffectiveDate", DateTime.UtcNow.ToString("yyyy-MM-dd") },
                    { "Description", $"Fallback Order - Kon JSON niet parsen:\n{berichtTekst}" }
                };

                if (!string.IsNullOrEmpty(accountId))
                {
                    orderData["AccountId"] = accountId;
                }

                var jsonBody = JsonSerializer.Serialize(orderData);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                // Maak een POST request naar Order object
                var url = $"{instanceUrl}/services/data/{apiVersion}/sobjects/Order";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                request.Content = content;

                Console.WriteLine($"📤 Verstuur fallback Order naar Salesforce...");

                // Verstuur request
                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                return VerwerkSalesforceResponse(response.StatusCode, responseBody);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fout bij fallback versturen: {ex.Message}");
                return new SalesforceResultaat
                {
                    IsSuccesvol = false,
                    StatusCode = 0,
                    IsHerhaalbaar = false,
                    Foutmelding = ex.Message
                };
            }
        }

        /// <summary>
        /// Interne methode om data naar Salesforce te versturen
        /// Maakt alleen Order (bestelling) aan - geen Lead meer
        /// </summary>
        private async Task<SalesforceResultaat> VerstuurNaarSalesforceAsync(OrderMessage bestelling, bool eerstePoging)
        {
            // Haal een geldig token op
            var accessToken = await _authService.HaalAccessTokenOpAsync();
            var instanceUrl = _configuratie["Salesforce:InstanceUrl"];
            var apiVersion = _configuratie["Salesforce:ApiVersion"] ?? "v60.0";

            // Check of configuratie compleet is
            if (string.IsNullOrEmpty(instanceUrl))
            {
                throw new Exception("❌ Salesforce:InstanceUrl ontbreekt in configuratie!");
            }

            // Maak/Update Order (bestelling) - ALLEEN Order, geen Lead meer
            Console.WriteLine("───────────────────────────────────────");
            Console.WriteLine("📦 Order aanmaken/bijwerken in Salesforce...");
            
            // Converteer status naar Salesforce Order Status
            var salesforceStatus = bestelling.Status.ToLower() switch
            {
                "pending" => "Draft",
                "processing" => "Activated",
                "shipped" => "Activated",
                "delivered" => "Closed",
                "completed" => "Closed",
                "cancelled" => "Cancelled",
                "failed" => "Cancelled",
                _ => "Draft"
            };

            // Maak items beschrijving voor Description veld (als beschikbaar)
            var itemsDescription = string.Join("\n", bestelling.Items.Select(i =>
                $"- {i.Brand} {i.ProductName} (Maat {i.Size}, {i.Color}) x{i.Quantity} = €{i.SubTotal}"));

            var orderDescription = $"Klant: {bestelling.UserName} ({bestelling.UserEmail})\n" +
                                  $"Status: {bestelling.Status}\n" +
                                  $"Totaal: €{bestelling.TotalPrice} ({bestelling.TotalQuantity} items)\n" +
                                  $"Verzendadres: {bestelling.ShippingAddress?.FullAddress}\n" +
                                  $"\nItems:\n{itemsDescription}";
            
            if (!string.IsNullOrWhiteSpace(bestelling.Notes))
            {
                orderDescription += $"\n\nNotities: {bestelling.Notes}";
            }

            var accountId = _configuratie["Salesforce:DefaultAccountId"];
            
            // WAARSCHUWING: AccountId is meestal VERPLICHT voor Order object
            if (string.IsNullOrEmpty(accountId))
            {
                Console.WriteLine("⚠️ WAARSCHUWING: Salesforce:DefaultAccountId ontbreekt in appsettings.json!");
                Console.WriteLine("⚠️ Het Order object vereist meestal een AccountId. Voeg dit toe aan appsettings.json:");
                Console.WriteLine("   \"Salesforce\": {");
                Console.WriteLine("     \"DefaultAccountId\": \"0015g00000ABC123\"  // Vervang met jouw Account ID");
                Console.WriteLine("   }");
                Console.WriteLine("⚠️ Order wordt toch geprobeerd aan te maken, maar kan falen...");
            }
            
            // Order data met alle beschikbare informatie
            var orderData = new Dictionary<string, object>
            {
                { "OrderNumber", bestelling.OrderId },
                { "Status", salesforceStatus },
                { "TotalAmount", bestelling.TotalPrice },
                { "EffectiveDate", bestelling.OrderDate.ToString("yyyy-MM-dd") }
            };

            // Voeg Description toe (als het Order object dit veld heeft)
            // Let op: Dit kan een custom field zijn, controleer in Salesforce of dit veld bestaat
            // Als het niet bestaat, wordt het genegeerd door Salesforce
            orderData["Description"] = orderDescription;

            // Voeg AccountId toe als beschikbaar (meestal VERPLICHT voor Order)
            if (!string.IsNullOrEmpty(accountId))
            {
                orderData["AccountId"] = accountId;
                Console.WriteLine($"✅ AccountId toegevoegd: {accountId}");
            }
            else
            {
                Console.WriteLine("❌ AccountId ontbreekt - Order kan falen als AccountId verplicht is!");
            }

            var orderJson = JsonSerializer.Serialize(orderData);
            var orderContent = new StringContent(orderJson, Encoding.UTF8, "application/json");

            Console.WriteLine($"📤 Verstuur Order {bestelling.OrderId} naar Salesforce...");
            Console.WriteLine($"📋 Status: {salesforceStatus}, TotalAmount: {bestelling.TotalPrice}");
            Console.WriteLine($"👤 Klant: {bestelling.UserName} ({bestelling.UserEmail})");
            Console.WriteLine($"📦 Items: {bestelling.TotalQuantity}");
            Console.WriteLine($"📄 Order Data: {orderJson}");

            // Gebruik direct POST om nieuwe Order aan te maken (UPSERT werkt mogelijk niet met OrderNumber)
            var postOrderUrl = $"{instanceUrl}/services/data/{apiVersion}/sobjects/Order";
            var postOrderRequest = new HttpRequestMessage(HttpMethod.Post, postOrderUrl);
            postOrderRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
            postOrderRequest.Content = orderContent;

            Console.WriteLine($"🔗 POST URL: {postOrderUrl}");
            
            var orderResponse = await _httpClient.SendAsync(postOrderRequest);
            var orderResponseBody = await orderResponse.Content.ReadAsStringAsync();
            
            Console.WriteLine($"📥 POST Response Status: {orderResponse.StatusCode}");
            Console.WriteLine($"📄 POST Response Body: {orderResponseBody}");

            // Als 401 (Unauthorized) en dit is de eerste poging, probeer met nieuw token
            if (orderResponse.StatusCode == HttpStatusCode.Unauthorized && eerstePoging)
            {
                Console.WriteLine("⚠️ 401 Unauthorized - Token verlopen, vernieuw token en probeer opnieuw...");
                accessToken = await _authService.ForceerTokenRefreshAsync();
                return await VerstuurNaarSalesforceAsync(bestelling, eerstePoging: false);
            }

            // Verwerk de Order response
            Console.WriteLine("───────────────────────────────────────");
            if (orderResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Order succesvol aangemaakt/bijgewerkt: {bestelling.OrderId}");
                Console.WriteLine($"📄 Response: {orderResponseBody}");
                Console.WriteLine($"✅ Order zou nu zichtbaar moeten zijn in Salesforce Orders tab!");
            }
            else
            {
                Console.WriteLine($"❌ Order fout: {orderResponse.StatusCode}");
                Console.WriteLine($"📄 Error details: {orderResponseBody}");
                
                // Als het een 400 Bad Request is, kan het zijn dat AccountId ontbreekt
                if (orderResponse.StatusCode == HttpStatusCode.BadRequest)
                {
                    Console.WriteLine("⚠️ 400 Bad Request - Mogelijk ontbreekt AccountId of andere verplichte velden");
                    Console.WriteLine("⚠️ Controleer in Salesforce welke velden verplicht zijn voor het Order object");
                    Console.WriteLine("⚠️ Voeg 'DefaultAccountId' toe aan appsettings.json in de Salesforce sectie");
                }
                
                // Als het een 404 is, probeer dan direct POST (zonder UPSERT)
                if (orderResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine("⚠️ 404 Not Found - Order bestaat niet, probeer direct POST...");
                }
            }

            return VerwerkSalesforceResponse(orderResponse.StatusCode, orderResponseBody);
        }

        /// <summary>
        /// Converteer Nederlandse landnaam naar Salesforce-compatibele country code
        /// Salesforce verwacht ISO country names (Engels) of ISO codes
        /// </summary>
        private string? ConverteerLandNaarSalesforceCode(string? landNaam)
        {
            if (string.IsNullOrWhiteSpace(landNaam))
            {
                return null;
            }

            // Normaliseer: verwijder spaties en converteer naar lowercase voor vergelijking
            var genormaliseerd = landNaam.Trim();

            // Mapping van Nederlandse landnamen naar Salesforce-compatibele codes
            var landMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "België", "Belgium" },
                { "Belgie", "Belgium" },
                { "Nederland", "Netherlands" },
                { "Duitsland", "Germany" },
                { "Frankrijk", "France" },
                { "Verenigd Koninkrijk", "United Kingdom" },
                { "VK", "United Kingdom" },
                { "UK", "United Kingdom" },
                { "Spanje", "Spain" },
                { "Italië", "Italy" },
                { "Italie", "Italy" },
                { "Portugal", "Portugal" },
                { "Oostenrijk", "Austria" },
                { "Zwitserland", "Switzerland" },
                { "Luxemburg", "Luxembourg" },
                { "Denemarken", "Denmark" },
                { "Zweden", "Sweden" },
                { "Noorwegen", "Norway" },
                { "Finland", "Finland" },
                { "Polen", "Poland" },
                { "Tsjechië", "Czech Republic" },
                { "Tsjechie", "Czech Republic" },
                { "Hongarije", "Hungary" },
                { "Roemenië", "Romania" },
                { "Roemenie", "Romania" },
                { "Griekenland", "Greece" },
                { "Ierland", "Ireland" },
                { "Verenigde Staten", "United States" },
                { "VS", "United States" },
                { "USA", "United States" },
                { "Canada", "Canada" },
                { "Australië", "Australia" },
                { "Australie", "Australia" },
                { "Nieuw-Zeeland", "New Zealand" },
                { "Japan", "Japan" },
                { "China", "China" },
                { "India", "India" },
                { "Brazilië", "Brazil" },
                { "Brazili", "Brazil" },
                { "Mexico", "Mexico" },
                { "Rusland", "Russia" },
                { "Turkije", "Turkey" },
                { "Zuid-Afrika", "South Africa" },
                { "Zuid Afrika", "South Africa" }
            };

            // Check of we een mapping hebben
            if (landMapping.TryGetValue(genormaliseerd, out var salesforceCode))
            {
                Console.WriteLine($"🌍 Land '{landNaam}' geconverteerd naar '{salesforceCode}' voor Salesforce");
                return salesforceCode;
            }

            // Als geen mapping gevonden, probeer het origineel (misschien is het al Engels)
            Console.WriteLine($"⚠️ Geen mapping gevonden voor '{landNaam}', gebruik origineel");
            return genormaliseerd;
        }

        /// <summary>
        /// Verwerk de response van Salesforce en bepaal wat er moet gebeuren
        /// </summary>
        private SalesforceResultaat VerwerkSalesforceResponse(HttpStatusCode statusCode, string responseBody)
        {
            var resultaat = new SalesforceResultaat
            {
                StatusCode = (int)statusCode,
                ResponseBody = responseBody
            };

            // Success codes (200-299)
            if (statusCode >= HttpStatusCode.OK && statusCode < HttpStatusCode.MultipleChoices)
            {
                Console.WriteLine($"✅ Salesforce success: {statusCode}");
                Console.WriteLine($"📄 Response: {responseBody}");
                resultaat.IsSuccesvol = true;
                resultaat.IsHerhaalbaar = false;
                return resultaat;
            }

            // 429 Too Many Requests - tijdelijk probleem
            if (statusCode == HttpStatusCode.TooManyRequests)
            {
                Console.WriteLine("⚠️ 429 Too Many Requests - Te veel requests, probeer later opnieuw");
                resultaat.IsSuccesvol = false;
                resultaat.IsHerhaalbaar = true; // NACK met requeue=true
                resultaat.Foutmelding = "Too Many Requests";
                return resultaat;
            }

            // 5xx Server Errors - tijdelijk probleem
            if ((int)statusCode >= 500)
            {
                Console.WriteLine($"⚠️ {statusCode} Server Error - Salesforce heeft een probleem, probeer later opnieuw");
                resultaat.IsSuccesvol = false;
                resultaat.IsHerhaalbaar = true; // NACK met requeue=true
                resultaat.Foutmelding = $"Server Error: {statusCode}";
                return resultaat;
            }

            // 400 Bad Request en andere 4xx - permanent probleem
            if ((int)statusCode >= 400 && (int)statusCode < 500)
            {
                Console.WriteLine($"❌ {statusCode} Client Error - Permanente fout, bericht gaat naar DLQ");
                Console.WriteLine($"📄 Error details: {responseBody}");
                resultaat.IsSuccesvol = false;
                resultaat.IsHerhaalbaar = false; // NACK met requeue=false
                resultaat.Foutmelding = $"Client Error: {statusCode}";
                return resultaat;
            }

            // Andere status codes
            Console.WriteLine($"⚠️ Onbekende status code: {statusCode}");
            resultaat.IsSuccesvol = false;
            resultaat.IsHerhaalbaar = false;
            resultaat.Foutmelding = $"Unknown status: {statusCode}";
            return resultaat;
        }
    }

    /// <summary>
    /// Resultaat van een Salesforce operatie
    /// </summary>
    public class SalesforceResultaat
    {
        /// <summary>
        /// Was de operatie succesvol?
        /// </summary>
        public bool IsSuccesvol { get; set; }

        /// <summary>
        /// HTTP status code
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Moet de operatie opnieuw geprobeerd worden? (voor NACK requeue beslissing)
        /// </summary>
        public bool IsHerhaalbaar { get; set; }

        /// <summary>
        /// Foutmelding (als er een fout was)
        /// </summary>
        public string? Foutmelding { get; set; }

        /// <summary>
        /// Response body van Salesforce
        /// </summary>
        public string? ResponseBody { get; set; }
    }
}

