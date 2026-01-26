using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BestelApp_API.Services
{
    /// <summary>
    /// Service voor het updaten van Order status in Salesforce
    /// Maakt directe API calls naar Salesforce
    /// </summary>
    public class SalesforceStatusService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SalesforceStatusService> _logger;
        private readonly HttpClient _httpClient;

        public SalesforceStatusService(IConfiguration configuration, ILogger<SalesforceStatusService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Update order status in Salesforce
        /// Zoekt Order op basis van Order Number in Description veld
        /// </summary>
        public async Task<bool> UpdateOrderStatusAsync(string orderNumber, string nieuweStatus)
        {
            try
            {
                _logger.LogInformation("🔄 Update order status in Salesforce: {OrderNumber} -> {Status}", orderNumber, nieuweStatus);

                // Haal Salesforce configuratie op
                var instanceUrl = _configuration["Salesforce:InstanceUrl"];
                var apiVersion = _configuration["Salesforce:ApiVersion"] ?? "v60.0";
                var clientId = _configuration["Salesforce:ClientId"];
                var clientSecret = _configuration["Salesforce:ClientSecret"];
                var refreshToken = _configuration["Salesforce:RefreshToken"];

                if (string.IsNullOrEmpty(instanceUrl) || string.IsNullOrEmpty(clientId) || 
                    string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogWarning("⚠️ Salesforce configuratie ontbreekt - status update overgeslagen");
                    return false;
                }

                // Haal access token op via OAuth
                var accessToken = await HaalAccessTokenOpAsync(clientId, clientSecret, refreshToken);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("❌ Kon geen Salesforce access token verkrijgen");
                    return false;
                }

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
                    _ => nieuweStatus
                };

                // Zoek Order in Salesforce via SOQL query
                // Probeer eerst op Description (meest betrouwbaar omdat we dit altijd vullen)
                // Escape single quotes in orderNumber voor veiligheid
                var escapedOrderNumber = orderNumber.Replace("'", "''");
                
                // Query 1: Zoek op Description met Order Number
                var soqlQuery = $"SELECT Id, Status, Description FROM Order WHERE Description LIKE '%Order Number: {escapedOrderNumber}%' OR Description LIKE '%{escapedOrderNumber}%' LIMIT 1";
                var encodedQuery = Uri.EscapeDataString(soqlQuery);
                var queryUrl = $"{instanceUrl}/services/data/{apiVersion}/query?q={encodedQuery}";

                var queryRequest = new HttpRequestMessage(HttpMethod.Get, queryUrl);
                queryRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

                _logger.LogInformation("🔍 Zoek Order in Salesforce: {OrderNumber}", orderNumber);
                _logger.LogInformation("📄 SOQL Query: {Query}", soqlQuery);

                var queryResponse = await _httpClient.SendAsync(queryRequest);
                var queryResponseBody = await queryResponse.Content.ReadAsStringAsync();

                if (!queryResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ Query gefaald: {StatusCode} - {Response}", queryResponse.StatusCode, queryResponseBody);
                    return false;
                }

                // Parse response om Order ID te vinden
                var queryJson = JsonDocument.Parse(queryResponseBody);
                var records = queryJson.RootElement.GetProperty("records");

                if (records.GetArrayLength() == 0)
                {
                    _logger.LogWarning("⚠️ Order niet gevonden in Salesforce via Description query: {OrderNumber}", orderNumber);
                    _logger.LogWarning("ℹ️  Probeer handmatig te zoeken in Salesforce op Order Number: {OrderNumber}", orderNumber);
                    return false;
                }

                var orderId = records[0].GetProperty("Id").GetString();
                _logger.LogInformation("✅ Order gevonden in Salesforce: {OrderId} (OrderNumber: {OrderNumber})", orderId, orderNumber);

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

                _logger.LogInformation("📤 Update Order status naar: {Status}", salesforceStatus);

                var updateResponse = await _httpClient.SendAsync(updateRequest);
                var updateResponseBody = await updateResponse.Content.ReadAsStringAsync();

                if (updateResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Order status succesvol geüpdatet in Salesforce: {OrderNumber} -> {Status}", orderNumber, salesforceStatus);
                    return true;
                }
                else
                {
                    _logger.LogWarning("⚠️ Order status update gefaald: {StatusCode} - {Response}", updateResponse.StatusCode, updateResponseBody);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Fout bij updaten order status in Salesforce: {OrderNumber}", orderNumber);
                return false;
            }
        }

        /// <summary>
        /// Haal OAuth access token op via refresh token
        /// </summary>
        private async Task<string?> HaalAccessTokenOpAsync(string clientId, string clientSecret, string refreshToken)
        {
            try
            {
                var tokenUrl = "https://login.salesforce.com/services/oauth2/token";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("refresh_token", refreshToken)
                });

                var response = await _httpClient.PostAsync(tokenUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tokenJson = JsonDocument.Parse(responseBody);
                    return tokenJson.RootElement.GetProperty("access_token").GetString();
                }

                _logger.LogWarning("⚠️ Kon geen access token verkrijgen: {StatusCode} - {Response}", response.StatusCode, responseBody);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Fout bij ophalen access token");
                return null;
            }
        }
    }
}
