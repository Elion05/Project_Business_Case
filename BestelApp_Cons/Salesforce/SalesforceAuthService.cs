using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace BestelApp_Cons.Salesforce
{
    /// <summary>
    /// Service om Salesforce access tokens te verkrijgen en te cachen
    /// Dit zorgt ervoor dat we niet bij elk bericht een nieuw token hoeven op te halen
    /// </summary>
    public class SalesforceAuthService
    {
        private readonly IConfiguration _configuratie;
        private readonly HttpClient _httpClient;
        
        // Hier bewaren we het token en wanneer het verloopt
        private string? _huidigAccessToken;
        private DateTime _tokenVerlooptOp;

        public SalesforceAuthService(IConfiguration configuratie)
        {
            _configuratie = configuratie;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Haal een geldig access token op
        /// Als het token nog geldig is, geef het terug
        /// Als het verlopen is, vernieuw het eerst
        /// </summary>
        public async Task<string> HaalAccessTokenOpAsync()
        {
            // Check: hebben we al een token en is het nog geldig?
            if (!string.IsNullOrEmpty(_huidigAccessToken) && DateTime.UtcNow < _tokenVerlooptOp)
            {
                Console.WriteLine("✅ Gebruik bestaand token (nog geldig)");
                return _huidigAccessToken;
            }

            // Token is verlopen of bestaat niet, haal een nieuw op
            Console.WriteLine("🔄 Token verlopen of niet gevonden, nieuwe token ophalen...");
            return await VernieuwTokenAsync();
        }

        /// <summary>
        /// Vernieuw het access token via het refresh token
        /// Deze methode wordt aangeroepen als het token verlopen is
        /// </summary>
        public async Task<string> VernieuwTokenAsync()
        {
            try
            {
                // Lees configuratie uit appsettings.json
                var loginUrl = _configuratie["Salesforce:LoginUrl"];
                var clientId = _configuratie["Salesforce:ClientId"];
                var clientSecret = _configuratie["Salesforce:ClientSecret"];
                var refreshToken = _configuratie["Salesforce:RefreshToken"];

                // Check of alle waardes er zijn
                if (string.IsNullOrEmpty(loginUrl) || string.IsNullOrEmpty(clientId) || 
                    string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(refreshToken))
                {
                    throw new Exception("❌ Salesforce configuratie ontbreekt in appsettings.json!");
                }

                // Maak de request body aan
                var requestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("refresh_token", refreshToken)
                });

                // Stuur request naar Salesforce
                var response = await _httpClient.PostAsync($"{loginUrl}/services/oauth2/token", requestBody);

                // Lees de response
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"❌ Salesforce token ophalen mislukt: {response.StatusCode} - {responseJson}");
                }

                // Parse het JSON antwoord
                using var document = JsonDocument.Parse(responseJson);
                var root = document.RootElement;

                // Haal access token en instance URL op
                var accessToken = root.GetProperty("access_token").GetString();
                var instanceUrl = root.GetProperty("instance_url").GetString();

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(instanceUrl))
                {
                    throw new Exception("❌ Access token of Instance URL niet gevonden in response!");
                }

                // Bewaar het token
                _huidigAccessToken = accessToken;
                
                // Salesforce tokens zijn standaard 2 uur geldig
                // We trekken er 5 minuten van af voor de zekerheid
                _tokenVerlooptOp = DateTime.UtcNow.AddMinutes(115);

                Console.WriteLine($"✅ Nieuw token verkregen! Geldig tot: {_tokenVerlooptOp:HH:mm:ss}");
                Console.WriteLine($"📍 Instance URL: {instanceUrl}");

                return accessToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fout bij token vernieuwen: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Forceer een token refresh (gebruikt bij 401 errors)
        /// </summary>
        public async Task<string> ForceerTokenRefreshAsync()
        {
            Console.WriteLine("🔄 Forceer token refresh (401 error ontvangen)...");
            _huidigAccessToken = null; // Reset het huidige token
            return await VernieuwTokenAsync();
        }
    }
}

