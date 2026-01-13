using System;
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
        /// </summary>
        public async Task<SalesforceResultaat> StuurFallbackBerichtAsync(string berichtTekst)
        {
            try
            {
                // Haal een geldig token op
                var accessToken = await _authService.HaalAccessTokenOpAsync();
                var instanceUrl = _configuratie["Salesforce:InstanceUrl"];
                var apiVersion = _configuratie["Salesforce:ApiVersion"] ?? "v60.0";

                // Maak een unieke ID op basis van timestamp
                var fallbackId = $"FALLBACK-{DateTime.UtcNow:yyyyMMddHHmmss}";

                // Maak JSON body voor Salesforce Lead (alleen Description veld)
                var salesforceData = new
                {
                    Company = "Unknown",  // Verplicht veld
                    LastName = "Fallback Order",  // Verplicht veld
                    Description = berichtTekst,
                    LeadSource = "RabbitMQ"
                };

                var jsonBody = JsonSerializer.Serialize(salesforceData);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                // Maak een POST request
                var url = $"{instanceUrl}/services/data/{apiVersion}/sobjects/Lead";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                request.Content = content;

                Console.WriteLine($"📤 Verstuur fallback bericht naar Salesforce...");

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

            // Maak JSON body voor Salesforce Lead object
            var salesforceData = new
            {
                Company = bestelling.Brand,  // Verplicht veld in Lead
                LastName = bestelling.Name,  // Verplicht veld in Lead
                Description = $"Order {bestelling.OrderId} - {bestelling.Brand} {bestelling.Name} - Maat: {bestelling.Size} - Prijs: €{bestelling.Price}",
                LeadSource = "RabbitMQ"
            };

            var jsonBody = JsonSerializer.Serialize(salesforceData);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // Maak een POST request om nieuw Lead aan te maken
            var url = $"{instanceUrl}/services/data/{apiVersion}/sobjects/Lead";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = content;

            Console.WriteLine($"📤 Verstuur bestelling {bestelling.OrderId} naar Salesforce...");
            Console.WriteLine($"🔗 URL: {url}");

            // Verstuur request
            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            // Als 401 (Unauthorized) en dit is de eerste poging, probeer met nieuw token
            if (response.StatusCode == HttpStatusCode.Unauthorized && eerstePoging)
            {
                Console.WriteLine("⚠️ 401 Unauthorized - Token verlopen, vernieuw token en probeer opnieuw...");

                // Forceer token refresh
                accessToken = await _authService.ForceerTokenRefreshAsync();

                // Probeer opnieuw (maar dit keer is het NIET de eerste poging meer)
                return await VerstuurNaarSalesforceAsync(bestelling, eerstePoging: false);
            }

            // Verwerk de response
            return VerwerkSalesforceResponse(response.StatusCode, responseBody);
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

