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
        /// Update order status in Salesforce
        /// Zoekt Order op basis van Order Number in Description veld
        /// </summary>
        public async Task<SalesforceResultaat> UpdateOrderStatusAsync(string orderNumber, string nieuweStatus)
        {
            try
            {
                Console.WriteLine("───────────────────────────────────────");
                Console.WriteLine($"🔄 Order status updaten in Salesforce...");
                Console.WriteLine($"   Order Number: {orderNumber}");
                Console.WriteLine($"   Nieuwe Status: {nieuweStatus}");

                // Haal een geldig token op
                var accessToken = await _authService.HaalAccessTokenOpAsync();
                var instanceUrl = _configuratie["Salesforce:InstanceUrl"];
                var apiVersion = _configuratie["Salesforce:ApiVersion"] ?? "v60.0";

                // Converteer status naar Salesforce Order Status
                var salesforceStatus = nieuweStatus.ToLower() switch
                {
                    "pending" => "Draft",
                    "processing" => "Activated",
                    "shipped" => "Activated",
                    "delivered" => "Closed",
                    "completed" => "Closed",
                    "cancelled" => "Cancelled",
                    "failed" => "Cancelled",
                    _ => nieuweStatus // Gebruik origineel als geen mapping
                };

                // Zoek Order in Salesforce via SOQL query (zoek op Order Number in Description)
                var soqlQuery = $"SELECT Id, Status, Description FROM Order WHERE Description LIKE '%Order Number: {orderNumber}%' LIMIT 1";
                var encodedQuery = Uri.EscapeDataString(soqlQuery);
                var queryUrl = $"{instanceUrl}/services/data/{apiVersion}/query?q={encodedQuery}";

                var queryRequest = new HttpRequestMessage(HttpMethod.Get, queryUrl);
                queryRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

                Console.WriteLine($"🔍 Zoek Order in Salesforce...");
                Console.WriteLine($"🔗 Query URL: {queryUrl}");

                var queryResponse = await _httpClient.SendAsync(queryRequest);
                var queryResponseBody = await queryResponse.Content.ReadAsStringAsync();

                if (!queryResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Query gefaald: {queryResponse.StatusCode}");
                    Console.WriteLine($"📄 Response: {queryResponseBody}");
                    return new SalesforceResultaat
                    {
                        IsSuccesvol = false,
                        StatusCode = (int)queryResponse.StatusCode,
                        IsHerhaalbaar = false,
                        Foutmelding = $"Query gefaald: {queryResponseBody}"
                    };
                }

                // Parse response om Order ID te vinden
                var queryJson = JsonDocument.Parse(queryResponseBody);
                var records = queryJson.RootElement.GetProperty("records");
                
                if (records.GetArrayLength() == 0)
                {
                    Console.WriteLine($"⚠️ Order niet gevonden in Salesforce: {orderNumber}");
                    return new SalesforceResultaat
                    {
                        IsSuccesvol = false,
                        StatusCode = 404,
                        IsHerhaalbaar = false,
                        Foutmelding = $"Order {orderNumber} niet gevonden in Salesforce"
                    };
                }

                var orderId = records[0].GetProperty("Id").GetString();
                Console.WriteLine($"✅ Order gevonden in Salesforce: {orderId}");

                // Update Order status
                var updateData = new Dictionary<string, object>
                {
                    { "Status", salesforceStatus }
                };

                var updateJson = JsonSerializer.Serialize(updateData);
                var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

                var updateUrl = $"{instanceUrl}/services/data/{apiVersion}/sobjects/Order/{orderId}";
                var updateRequest = new HttpRequestMessage(HttpMethod.Patch, updateUrl);
                updateRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
                updateRequest.Content = updateContent;

                Console.WriteLine($"📤 Update Order status naar: {salesforceStatus}");
                Console.WriteLine($"🔗 PATCH URL: {updateUrl}");

                var updateResponse = await _httpClient.SendAsync(updateRequest);
                var updateResponseBody = await updateResponse.Content.ReadAsStringAsync();

                Console.WriteLine($"📥 PATCH Response Status: {updateResponse.StatusCode}");
                Console.WriteLine($"📄 PATCH Response Body: {updateResponseBody}");

                if (updateResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Order status succesvol geüpdatet in Salesforce!");
                    return new SalesforceResultaat
                    {
                        IsSuccesvol = true,
                        StatusCode = (int)updateResponse.StatusCode,
                        IsHerhaalbaar = false,
                        ResponseBody = updateResponseBody
                    };
                }
                else
                {
                    Console.WriteLine($"❌ Order status update gefaald: {updateResponse.StatusCode}");
                    return VerwerkSalesforceResponse(updateResponse.StatusCode, updateResponseBody);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fout bij updaten order status: {ex.Message}");
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

                // Maak JSON body voor Salesforce Order - alleen beschrijfbare velden
                var orderData = new Dictionary<string, object>
                {
                    { "Status", "Draft" },
                    { "EffectiveDate", DateTime.UtcNow.ToString("yyyy-MM-dd") },
                    { "Description", $"Fallback Order Number: {fallbackOrderId}\nTotaal: €0\n\nKon JSON niet parsen:\n{berichtTekst}" }
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
        /// Maakt zowel Order (bestelling) als Lead aan
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
            
            // Order data - alleen beschrijfbare velden gebruiken
            // OrderNumber is vaak read-only in Salesforce, dus we slaan deze over
            // De Order Number wordt automatisch gegenereerd door Salesforce
            // TotalAmount proberen we wel mee te sturen (sommige Salesforce orgs accepteren dit)
            var orderData = new Dictionary<string, object>
            {
                { "Status", salesforceStatus },
                { "EffectiveDate", bestelling.OrderDate.ToString("yyyy-MM-dd") }
            };

            // TotalAmount is read-only in Salesforce - wordt NIET meegestuurd
            // TotalAmount wordt automatisch berekend op basis van Order Products (die we later aanmaken)
            Console.WriteLine($"ℹ️  TotalAmount wordt niet meegestuurd (read-only in Salesforce)");
            Console.WriteLine($"ℹ️  TotalAmount wordt automatisch berekend op basis van Order Products");

            // Voeg Description toe met alle order informatie inclusief Order Number en Total
            // Dit is het enige veld waar we alle details kunnen opslaan
            var fullDescription = $"Order Number: {bestelling.OrderId}\n" +
                                 $"Totaal Bedrag: €{bestelling.TotalPrice}\n\n" +
                                 orderDescription;
            orderData["Description"] = fullDescription;

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

            // Voeg ShippingAddress velden toe
            if (bestelling.ShippingAddress != null)
            {
                if (!string.IsNullOrWhiteSpace(bestelling.ShippingAddress.Address))
                {
                    orderData["ShippingStreet"] = bestelling.ShippingAddress.Address;
                    Console.WriteLine($"📍 ShippingStreet toegevoegd: {bestelling.ShippingAddress.Address}");
                }

                if (!string.IsNullOrWhiteSpace(bestelling.ShippingAddress.City))
                {
                    orderData["ShippingCity"] = bestelling.ShippingAddress.City;
                    Console.WriteLine($"📍 ShippingCity toegevoegd: {bestelling.ShippingAddress.City}");
                }

                if (!string.IsNullOrWhiteSpace(bestelling.ShippingAddress.PostalCode))
                {
                    orderData["ShippingPostalCode"] = bestelling.ShippingAddress.PostalCode;
                    Console.WriteLine($"📍 ShippingPostalCode toegevoegd: {bestelling.ShippingAddress.PostalCode}");
                }

                if (!string.IsNullOrWhiteSpace(bestelling.ShippingAddress.Country))
                {
                    var countryCode = ConverteerLandNaarSalesforceCode(bestelling.ShippingAddress.Country);
                    if (!string.IsNullOrWhiteSpace(countryCode))
                    {
                        orderData["ShippingCountry"] = countryCode;
                        Console.WriteLine($"📍 ShippingCountry toegevoegd: {countryCode}");
                    }
                }
            }

            var orderJson = JsonSerializer.Serialize(orderData);
            var orderContent = new StringContent(orderJson, Encoding.UTF8, "application/json");

            Console.WriteLine($"📤 Verstuur Order {bestelling.OrderId} naar Salesforce...");
            Console.WriteLine($"📋 Status: {salesforceStatus}, Totaal: €{bestelling.TotalPrice}");
            Console.WriteLine($"👤 Klant: {bestelling.UserName} ({bestelling.UserEmail})");
            Console.WriteLine($"📦 Items: {bestelling.TotalQuantity}");
            Console.WriteLine($"📄 Order Data (zonder read-only velden): {orderJson}");
            Console.WriteLine($"ℹ️  OrderNumber en TotalAmount worden niet meegestuurd (read-only in Salesforce)");
            Console.WriteLine($"ℹ️  TotalAmount wordt automatisch berekend op basis van Order Products");
            Console.WriteLine($"ℹ️  Order informatie staat ook in de Description veld");

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

                // Haal Salesforce Order ID uit response (als beschikbaar)
                string? orderSalesforceId = null;
                try
                {
                    var responseJson = JsonDocument.Parse(orderResponseBody);
                    if (responseJson.RootElement.TryGetProperty("id", out var idElement))
                    {
                        orderSalesforceId = idElement.GetString();
                    }
                }
                catch
                {
                    // Als we de ID niet kunnen parsen, gebruik de OrderId als fallback
                    orderSalesforceId = bestelling.OrderId;
                }

                // Maak Order Products aan om het bedrag te berekenen
                // In Salesforce wordt TotalAmount meestal berekend op basis van Order Products
                if (!string.IsNullOrEmpty(orderSalesforceId))
                {
                    var productsSuccesvol = await MaakOrderProductsAanAsync(bestelling, orderSalesforceId, accessToken, instanceUrl, apiVersion);
                    if (!productsSuccesvol)
                    {
                        Console.WriteLine("⚠️ WAARSCHUWING: Order Products konden niet worden aangemaakt");
                        Console.WriteLine("⚠️ TotalAmount blijft mogelijk 0 in Salesforce");
                        Console.WriteLine("ℹ️  Dit kan zijn omdat Product2Id verplicht is - maak eerst Products aan in Salesforce");
                    }
                }
                else
                {
                    Console.WriteLine("⚠️ Kon Order Products niet aanmaken - Order ID niet beschikbaar");
                }

                // Maak ook een Lead aan na succesvol Order aanmaken
                var leadSuccesvol = await MaakLeadAanAsync(bestelling, orderSalesforceId ?? bestelling.OrderId);

                if (!leadSuccesvol)
                {
                    Console.WriteLine("⚠️ WAARSCHUWING: Order is succesvol aangemaakt, maar Lead aanmaken is gefaald");
                    Console.WriteLine("⚠️ Dit is geen kritieke fout - Order blijft geldig");
                }
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
        /// Zoek of maak een Product aan in Salesforce
        /// Zoekt eerst op ProductCode (SKU), als niet gevonden wordt Product aangemaakt
        /// </summary>
        private async Task<string?> ZoekOfMaakProductAanAsync(OrderItemMessage item, string accessToken, string instanceUrl, string apiVersion)
        {
            try
            {
                // Genereer SKU voor dit item (als we die hebben)
                // Format: {Brand}-{ProductName}-{Size}-{Color}
                var brandClean = (item.Brand ?? "UNKNOWN").ToUpper().Replace(" ", "-").Replace("/", "-").Replace("\\", "-");
                var nameClean = (item.ProductName ?? "PRODUCT").ToUpper().Replace(" ", "-").Replace("/", "-").Replace("\\", "-");
                var colorClean = (item.Color ?? "UNKNOWN").Trim().ToUpper().Replace(" ", "-").Replace("/", "-").Replace("\\", "-");
                var productCode = $"{brandClean}-{nameClean}-{item.Size}-{colorClean}";

                // Product naam voor Salesforce
                var productName = $"{item.Brand} {item.ProductName} - Maat {item.Size}, {item.Color}";
                var productDescription = $"Product van BestelApp\nMerk: {item.Brand}\nProduct: {item.ProductName}\nMaat: {item.Size}\nKleur: {item.Color}";

                // Zoek eerst of Product al bestaat op basis van ProductCode
                var soqlQuery = $"SELECT Id, Name, ProductCode FROM Product2 WHERE ProductCode = '{productCode.Replace("'", "''")}' LIMIT 1";
                var encodedQuery = Uri.EscapeDataString(soqlQuery);
                var queryUrl = $"{instanceUrl}/services/data/{apiVersion}/query?q={encodedQuery}";

                var queryRequest = new HttpRequestMessage(HttpMethod.Get, queryUrl);
                queryRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

                Console.WriteLine($"🔍 Zoek Product in Salesforce: {productCode}");

                var queryResponse = await _httpClient.SendAsync(queryRequest);
                var queryResponseBody = await queryResponse.Content.ReadAsStringAsync();

                if (queryResponse.IsSuccessStatusCode)
                {
                    var queryJson = JsonDocument.Parse(queryResponseBody);
                    var records = queryJson.RootElement.GetProperty("records");

                    if (records.GetArrayLength() > 0)
                    {
                        var productId = records[0].GetProperty("Id").GetString();
                        Console.WriteLine($"✅ Product gevonden in Salesforce: {productId} ({productCode})");
                        return productId;
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ Query gefaald: {queryResponse.StatusCode} - {queryResponseBody}");
                }

                // Product niet gevonden - maak nieuw Product aan
                var autoCreate = _configuratie["Salesforce:AutoCreateProducts"] ?? "true";
                if (autoCreate.ToLower() != "true")
                {
                    Console.WriteLine($"ℹ️  AutoCreateProducts is uitgeschakeld - Product niet aangemaakt: {productCode}");
                    return null;
                }

                Console.WriteLine($"📦 Product niet gevonden - maak nieuw Product aan: {productCode}");

                var productData = new Dictionary<string, object>
                {
                    { "Name", productName },
                    { "ProductCode", productCode },
                    { "IsActive", true },
                    { "Description", productDescription }
                };

                var productJson = JsonSerializer.Serialize(productData);
                var productContent = new StringContent(productJson, Encoding.UTF8, "application/json");

                var createProductUrl = $"{instanceUrl}/services/data/{apiVersion}/sobjects/Product2";
                var createProductRequest = new HttpRequestMessage(HttpMethod.Post, createProductUrl);
                createProductRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
                createProductRequest.Content = productContent;

                Console.WriteLine($"📤 Maak Product aan in Salesforce: {productName}");
                Console.WriteLine($"📄 Product Data: {productJson}");

                var createResponse = await _httpClient.SendAsync(createProductRequest);
                var createResponseBody = await createResponse.Content.ReadAsStringAsync();

                if (createResponse.IsSuccessStatusCode)
                {
                    var responseJson = JsonDocument.Parse(createResponseBody);
                    var productId = responseJson.RootElement.GetProperty("id").GetString();
                    Console.WriteLine($"✅ Product succesvol aangemaakt in Salesforce: {productId} ({productCode})");
                    return productId;
                }
                else
                {
                    Console.WriteLine($"❌ Fout bij aanmaken Product: {createResponse.StatusCode}");
                    Console.WriteLine($"📄 Error: {createResponseBody}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Fout bij zoeken/aanmaken Product: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Maak Order Products aan in Salesforce om het bedrag te berekenen
        /// Order Products zijn nodig zodat TotalAmount automatisch wordt berekend
        /// </summary>
        private async Task<bool> MaakOrderProductsAanAsync(OrderMessage bestelling, string orderSalesforceId, string accessToken, string instanceUrl, string apiVersion)
        {
            try
            {
                Console.WriteLine("───────────────────────────────────────");
                Console.WriteLine("📦 Order Products aanmaken in Salesforce...");
                Console.WriteLine($"   Order ID: {orderSalesforceId}");
                Console.WriteLine($"   Items: {bestelling.Items.Count}");

                // Voor elk item in de order, maak een Order Product aan
                int successCount = 0;
                foreach (var item in bestelling.Items)
                {
                    try
                    {
                        // Zoek of maak Product aan in Salesforce
                        var product2Id = await ZoekOfMaakProductAanAsync(item, accessToken, instanceUrl, apiVersion);
                        
                        if (string.IsNullOrEmpty(product2Id))
                        {
                            Console.WriteLine($"⚠️ Kon geen Product2Id verkrijgen voor item: {item.Brand} {item.ProductName}");
                            Console.WriteLine($"⚠️ Order Product wordt overgeslagen - TotalAmount kan niet worden berekend");
                            continue;
                        }

                        // Order Product data
                        // ListPrice is verplicht in Salesforce - gebruik dezelfde prijs als UnitPrice
                        var orderProductData = new Dictionary<string, object>
                        {
                            { "OrderId", orderSalesforceId },
                            { "Product2Id", product2Id },
                            { "Quantity", item.Quantity },
                            { "UnitPrice", item.Price }, // Prijs per stuk voor deze order
                            { "ListPrice", item.Price }  // ListPrice is verplicht - gebruik dezelfde prijs
                        };

                        // Optioneel: Product naam in Description
                        var productDescription = $"{item.Brand} {item.ProductName} - Maat {item.Size}, Kleur {item.Color}";
                        orderProductData["Description"] = productDescription;

                        var productJson = JsonSerializer.Serialize(orderProductData);
                        var productContent = new StringContent(productJson, Encoding.UTF8, "application/json");

                        var productUrl = $"{instanceUrl}/services/data/{apiVersion}/sobjects/OrderItem";
                        var productRequest = new HttpRequestMessage(HttpMethod.Post, productUrl);
                        productRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
                        productRequest.Content = productContent;

                        var productResponse = await _httpClient.SendAsync(productRequest);
                        var productResponseBody = await productResponse.Content.ReadAsStringAsync();

                        if (productResponse.IsSuccessStatusCode)
                        {
                            successCount++;
                            Console.WriteLine($"✅ Order Product aangemaakt: {productDescription}");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Order Product gefaald: {productResponse.StatusCode}");
                            Console.WriteLine($"📄 Error: {productResponseBody}");
                            // Als Product2Id verplicht is, kunnen we het niet aanmaken
                            // Dit is niet kritiek - TotalAmount blijft dan 0 of wordt handmatig gezet
                            Console.WriteLine($"ℹ️  Tip: Maak een generic Product aan in Salesforce en voeg Product2Id toe aan configuratie");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Fout bij aanmaken Order Product: {ex.Message}");
                    }
                }

                if (successCount > 0)
                {
                    Console.WriteLine($"✅ {successCount} van {bestelling.Items.Count} Order Products succesvol aangemaakt");
                    Console.WriteLine($"💰 TotalAmount zou nu automatisch moeten worden berekend in Salesforce");
                    return true;
                }
                else
                {
                    Console.WriteLine($"⚠️ Geen Order Products aangemaakt - TotalAmount blijft mogelijk 0");
                    Console.WriteLine($"ℹ️  Dit kan zijn omdat Product2Id verplicht is in jouw Salesforce org");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Fout bij aanmaken Order Products: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Maak een Lead aan in Salesforce met klantinformatie
        /// Wordt aangeroepen na succesvol Order aanmaken
        /// </summary>
        private async Task<bool> MaakLeadAanAsync(OrderMessage bestelling, string orderSalesforceId)
        {
            try
            {
                Console.WriteLine("───────────────────────────────────────");
                Console.WriteLine("👤 Lead aanmaken in Salesforce...");

                // Haal een geldig token op
                var accessToken = await _authService.HaalAccessTokenOpAsync();
                var instanceUrl = _configuratie["Salesforce:InstanceUrl"];
                var apiVersion = _configuratie["Salesforce:ApiVersion"] ?? "v60.0";

                // Splits UserName in FirstName en LastName
                var naamDelen = bestelling.UserName?.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                var firstName = naamDelen.Length > 0 ? naamDelen[0] : bestelling.UserName ?? "Onbekend";
                var lastName = naamDelen.Length > 1 ? naamDelen[1] : "";

                // Maak items beschrijving
                var itemsDescription = string.Join("\n", bestelling.Items.Select(i =>
                    $"- {i.Brand} {i.ProductName} (Maat {i.Size}, {i.Color}) x{i.Quantity} = €{i.SubTotal}"));

                var leadDescription = $"Order Number: {bestelling.OrderId}\n" +
                                    $"Salesforce Order ID: {orderSalesforceId}\n" +
                                    $"Status: {bestelling.Status}\n" +
                                    $"Totaal: €{bestelling.TotalPrice} ({bestelling.TotalQuantity} items)\n" +
                                    $"\nItems:\n{itemsDescription}";

                if (!string.IsNullOrWhiteSpace(bestelling.Notes))
                {
                    leadDescription += $"\n\nNotities: {bestelling.Notes}";
                }

                // Lead data structuur
                var leadData = new Dictionary<string, object>
                {
                    { "FirstName", firstName },
                    { "Email", bestelling.UserEmail },
                    { "Company", $"BestelApp Klant - {bestelling.OrderId}" },
                    { "LeadSource", "BestelApp Web" },
                    { "Description", leadDescription }
                };

                // Voeg LastName toe als beschikbaar
                if (!string.IsNullOrWhiteSpace(lastName))
                {
                    leadData["LastName"] = lastName;
                }
                else
                {
                    // Als er geen LastName is, gebruik "Klant" als fallback
                    leadData["LastName"] = "Klant";
                }

                // Voeg adres toe als beschikbaar
                if (bestelling.ShippingAddress != null)
                {
                    if (!string.IsNullOrWhiteSpace(bestelling.ShippingAddress.Address))
                    {
                        leadData["Street"] = bestelling.ShippingAddress.Address;
                    }

                    if (!string.IsNullOrWhiteSpace(bestelling.ShippingAddress.City))
                    {
                        leadData["City"] = bestelling.ShippingAddress.City;
                    }

                    if (!string.IsNullOrWhiteSpace(bestelling.ShippingAddress.PostalCode))
                    {
                        leadData["PostalCode"] = bestelling.ShippingAddress.PostalCode;
                    }

                    if (!string.IsNullOrWhiteSpace(bestelling.ShippingAddress.Country))
                    {
                        // Converteer landnaam naar Salesforce-compatibele code
                        var countryCode = ConverteerLandNaarSalesforceCode(bestelling.ShippingAddress.Country);
                        if (!string.IsNullOrWhiteSpace(countryCode))
                        {
                            leadData["Country"] = countryCode;
                        }
                    }
                }

                var leadJson = JsonSerializer.Serialize(leadData);
                var leadContent = new StringContent(leadJson, Encoding.UTF8, "application/json");

                Console.WriteLine($"📤 Verstuur Lead naar Salesforce...");
                Console.WriteLine($"👤 Naam: {firstName} {lastName}");
                Console.WriteLine($"📧 Email: {bestelling.UserEmail}");
                Console.WriteLine($"📄 Lead Data: {leadJson}");

                // Maak POST request naar Lead object
                var postLeadUrl = $"{instanceUrl}/services/data/{apiVersion}/sobjects/Lead";
                var postLeadRequest = new HttpRequestMessage(HttpMethod.Post, postLeadUrl);
                postLeadRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
                postLeadRequest.Content = leadContent;

                Console.WriteLine($"🔗 POST URL: {postLeadUrl}");

                var leadResponse = await _httpClient.SendAsync(postLeadRequest);
                var leadResponseBody = await leadResponse.Content.ReadAsStringAsync();

                Console.WriteLine($"📥 POST Response Status: {leadResponse.StatusCode}");
                Console.WriteLine($"📄 POST Response Body: {leadResponseBody}");

                if (leadResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Lead succesvol aangemaakt voor order: {bestelling.OrderId}");
                    Console.WriteLine($"✅ Lead zou nu zichtbaar moeten zijn in Salesforce Leads tab!");
                    return true;
                }
                else
                {
                    Console.WriteLine($"⚠️ Lead aanmaken gefaald: {leadResponse.StatusCode}");
                    Console.WriteLine($"📄 Error details: {leadResponseBody}");
                    Console.WriteLine($"⚠️ Order is wel succesvol aangemaakt, maar Lead niet");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Fout bij Lead aanmaken: {ex.Message}");
                Console.WriteLine($"⚠️ Order is wel succesvol aangemaakt, maar Lead niet");
                return false;
            }
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

