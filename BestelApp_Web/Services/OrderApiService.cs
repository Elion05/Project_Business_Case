using System.Text;
using System.Text.Json;
using BestelApp_Models;

namespace BestelApp_Web.Services
{
    /// <summary>
    /// Service om HTTP calls te doen naar de Backend API
    /// WebApp heeft GEEN directe RabbitMQ of Salesforce connectie meer!
    /// </summary>
    public class OrderApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OrderApiService> _logger;

        public OrderApiService(HttpClient httpClient, IConfiguration configuration, ILogger<OrderApiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            // Configureer base URL voor Backend API
            var apiBaseUrl = _configuration["BackendApi:BaseUrl"] ?? "https://localhost:7001";
            _httpClient.BaseAddress = new Uri(apiBaseUrl);
        }

        /// <summary>
        /// Plaats een bestelling via de Backend API
        /// </summary>
        public async Task<bool> PlaceOrderAsync(Shoe shoe, string gebruikerId)
        {
            try
            {
                _logger.LogInformation("Verstuur bestelling naar Backend API: {Brand} {Name} voor gebruiker {UserId}",
                    shoe.Brand, shoe.Name, gebruikerId);

                // Maak simpel request object (vermijdt validatie problemen)
                var quickOrder = new
                {
                    UserId = gebruikerId, // Stuur gebruikerId mee!
                    Name = shoe.Name,
                    Brand = shoe.Brand,
                    Price = shoe.Price,
                    Size = shoe.Size,
                    Color = shoe.Color
                };

                // Serialize naar JSON
                var jsonContent = JsonSerializer.Serialize(quickOrder);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // POST naar Backend API
                var response = await _httpClient.PostAsync("/api/orders", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Bestelling succesvol geplaatst. Response: {Response}", responseBody);
                    return true;
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Backend API fout: {StatusCode} - {Error}", response.StatusCode, errorBody);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij communicatie met Backend API");
                return false;
            }
        }

        /// <summary>
        /// Health check van de Backend API
        /// </summary>
        public async Task<bool> IsApiHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/orders/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
