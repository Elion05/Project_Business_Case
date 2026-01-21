using System.Text;
using System.Text.Json;

namespace BestelApp_Web.Services
{
    /// <summary>
    /// Service om Cart API calls te doen
    /// </summary>
    public class CartApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CartApiService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CartApiService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;

            var apiBaseUrl = _configuration["BackendApi:BaseUrl"] ?? "https://localhost:7001";
            _httpClient.BaseAddress = new Uri(apiBaseUrl);
        }

        /// <summary>
        /// Haal cart op voor ingelogde user
        /// </summary>
        public async Task<CartResponse?> GetCartAsync()
        {
            try
            {
                await ForwardCookiesAsync();
                var response = await _httpClient.GetAsync("/api/cart");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CartResponse>();
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Cart ophalen mislukt: {StatusCode} - {Body}", response.StatusCode, errorBody);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen cart");
                return null;
            }
        }

        /// <summary>
        /// Voeg item toe aan cart
        /// </summary>
        public async Task<CartAddResult> AddToCartAsync(long shoeVariantId, int quantity = 1)
        {
            try
            {
                await ForwardCookiesAsync();
                var request = new { shoeVariantId, quantity };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("/api/cart/items", content);
                if (response.IsSuccessStatusCode)
                {
                    // API geeft cartItemCount terug
                    try
                    {
                        var body = await response.Content.ReadFromJsonAsync<CartAddResult>();
                        if (body != null)
                        {
                            body.Gelukt = true;
                            return body;
                        }
                    }
                    catch
                    {
                        // negeer parse fouten, we geven generiek success terug
                    }

                    return new CartAddResult { Gelukt = true, Bericht = "Item toegevoegd aan cart" };
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Cart add mislukt: {StatusCode} - {Body}", response.StatusCode, errorBody);
                return new CartAddResult { Gelukt = false, Bericht = "Cart add mislukt" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij toevoegen aan cart");
                return new CartAddResult { Gelukt = false, Bericht = "Fout bij toevoegen aan cart" };
            }
        }

        public class CartAddResult
        {
            public bool Gelukt { get; set; } = false;
            public string Bericht { get; set; } = string.Empty;
            public int CartItemCount { get; set; } = 0;
        }

        /// <summary>
        /// Update aantal van cart item
        /// </summary>
        public async Task<bool> UpdateCartItemAsync(long itemId, int quantity)
        {
            try
            {
                await ForwardCookiesAsync();
                var request = new { quantity };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PutAsync($"/api/cart/items/{itemId}", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij updaten cart item");
                return false;
            }
        }

        /// <summary>
        /// Verwijder item uit cart
        /// </summary>
        public async Task<bool> RemoveFromCartAsync(long itemId)
        {
            try
            {
                await ForwardCookiesAsync();
                var response = await _httpClient.DeleteAsync($"/api/cart/items/{itemId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij verwijderen uit cart");
                return false;
            }
        }

        /// <summary>
        /// Leeg hele cart
        /// </summary>
        public async Task<bool> ClearCartAsync()
        {
            try
            {
                await ForwardCookiesAsync();
                var response = await _httpClient.DeleteAsync("/api/cart");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij legen cart");
                return false;
            }
        }

        /// <summary>
        /// Forward authentication cookies van HttpContext naar API request
        /// </summary>
        private Task ForwardCookiesAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Request.Headers.ContainsKey("Cookie") == true)
            {
                var cookies = httpContext.Request.Headers["Cookie"].ToString();
                _httpClient.DefaultRequestHeaders.Remove("Cookie");
                _httpClient.DefaultRequestHeaders.Add("Cookie", cookies);
            }
            return Task.CompletedTask;
        }

        // Response models
        public class CartResponse
        {
            public long Id { get; set; }
            public string UserId { get; set; } = string.Empty;
            public List<CartItemResponse> Items { get; set; } = new();
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public int TotalItems { get; set; }
            public decimal TotalPrice { get; set; }
        }

        public class CartItemResponse
        {
            public long Id { get; set; }
            public long CartId { get; set; }
            public long ShoeVariantId { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
            public DateTime AddedAt { get; set; }
            public ShoeVariantResponse ShoeVariant { get; set; } = null!;
        }

        public class ShoeVariantResponse
        {
            public long Id { get; set; }
            public long ShoeId { get; set; }
            public int Size { get; set; }
            public string Color { get; set; } = string.Empty;
            public int Stock { get; set; }
            public ShoeResponse Shoe { get; set; } = null!;
        }

        public class ShoeResponse
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public string? ImageUrl { get; set; }
            public CategoryResponse? Category { get; set; }
        }

        public class CategoryResponse
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}
