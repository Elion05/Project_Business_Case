using System.Text;
using System.Text.Json;

namespace BestelApp_Web.Services
{
    /// <summary>
    /// Service om Favorites API calls te doen
    /// </summary>
    public class FavoritesApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FavoritesApiService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FavoritesApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<FavoritesApiService> logger,
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
        /// Haal alle favorieten op voor ingelogde user
        /// </summary>
        public async Task<List<FavoriteResponse>?> GetFavoritesAsync()
        {
            try
            {
                // Forward authentication cookies van HttpContext naar API request
                await ForwardCookiesAsync();
                var response = await _httpClient.GetAsync("/api/favorites");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<FavoriteResponse>>();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen favorieten");
                return null;
            }
        }

        /// <summary>
        /// Voeg product toe aan favorieten
        /// </summary>
        public async Task<bool> AddToFavoritesAsync(long shoeId)
        {
            try
            {
                await ForwardCookiesAsync();
                var response = await _httpClient.PostAsync($"/api/favorites/{shoeId}", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij toevoegen favoriet");
                return false;
            }
        }

        /// <summary>
        /// Verwijder uit favorieten
        /// </summary>
        public async Task<bool> RemoveFromFavoritesAsync(long shoeId)
        {
            try
            {
                await ForwardCookiesAsync();
                var response = await _httpClient.DeleteAsync($"/api/favorites/{shoeId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij verwijderen favoriet");
                return false;
            }
        }

        /// <summary>
        /// Check of product favoriet is
        /// </summary>
        public async Task<bool> IsFavoriteAsync(long shoeId)
        {
            try
            {
                await ForwardCookiesAsync();
                var response = await _httpClient.GetAsync($"/api/favorites/check/{shoeId}");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<IsFavoriteResponse>();
                    return result?.IsFavorite ?? false;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij checken favoriet status");
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
    }

    public class FavoriteResponse
    {
        public long Id { get; set; }
        public long ShoeId { get; set; }
        public DateTime AddedAt { get; set; }
        public ShoeInfo Shoe { get; set; } = new();
    }

    public class ShoeInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<VariantInfo> Variants { get; set; } = new();
    }

    public class VariantInfo
    {
        public long Id { get; set; }
        public int Size { get; set; }
        public string Color { get; set; } = string.Empty;
        public int Stock { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class IsFavoriteResponse
    {
        public bool IsFavorite { get; set; }
    }
}
