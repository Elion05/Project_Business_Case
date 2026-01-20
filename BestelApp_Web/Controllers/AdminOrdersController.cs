using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BestelApp_Web.Controllers
{
    /// <summary>
    /// Admin bestellingen overzicht (via Backend API)
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AdminOrdersController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminOrdersController> _logger;
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminOrdersController(
            IConfiguration configuration,
            ILogger<AdminOrdersController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;

            _httpClient = new HttpClient();
            var apiBaseUrl = _configuration["BackendApi:BaseUrl"] ?? "https://localhost:7001";
            _httpClient.BaseAddress = new Uri(apiBaseUrl);
        }

        // GET: AdminOrders
        public async Task<IActionResult> Index()
        {
            try
            {
                ForwardCookies();
                var response = await _httpClient.GetAsync("/api/orders/all");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Admin orders ophalen mislukt: {StatusCode}", response.StatusCode);
                    return View(new List<AdminOrderListItemViewModel>());
                }

                var json = await response.Content.ReadAsStringAsync();
                var orders = JsonSerializer.Deserialize<List<AdminOrderListItemViewModel>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AdminOrderListItemViewModel>();

                return View(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen admin orders");
                return View(new List<AdminOrderListItemViewModel>());
            }
        }

        // GET: AdminOrders/Details/5
        public async Task<IActionResult> Details(long id)
        {
            try
            {
                ForwardCookies();
                var response = await _httpClient.GetAsync($"/api/orders/{id}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Order detail ophalen mislukt: {StatusCode}", response.StatusCode);
                    TempData["FoutBericht"] = "Order niet gevonden of geen toegang.";
                    return RedirectToAction(nameof(Index));
                }

                var json = await response.Content.ReadAsStringAsync();
                var order = JsonSerializer.Deserialize<AdminOrderDetailViewModel>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (order == null)
                {
                    TempData["FoutBericht"] = "Order kon niet geladen worden.";
                    return RedirectToAction(nameof(Index));
                }

                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen order detail");
                TempData["FoutBericht"] = "Er ging iets fout.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: AdminOrders/Send/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(long id)
        {
            try
            {
                ForwardCookies();
                var response = await _httpClient.PostAsync($"/api/orders/{id}/send", content: null);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessBericht"] = "Order is verstuurd naar RabbitMQ.";
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    TempData["FoutBericht"] = $"Kon order niet versturen. ({response.StatusCode})";
                    _logger.LogError("Order send failed: {Status} - {Body}", response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij versturen order naar queue");
                TempData["FoutBericht"] = "Er ging iets fout bij het versturen.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private void ForwardCookies()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Request.Headers.ContainsKey("Cookie") == true)
            {
                var cookies = httpContext.Request.Headers["Cookie"].ToString();
                _httpClient.DefaultRequestHeaders.Remove("Cookie");
                _httpClient.DefaultRequestHeaders.Add("Cookie", cookies);
            }
        }
    }

    // =========================
    // ViewModels (match API JSON)
    // =========================
    public class AdminOrderListItemViewModel
    {
        public long Id { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public int TotalQuantity { get; set; }
        public bool IsSentToQueue { get; set; }
        public int ItemCount { get; set; }
    }

    public class AdminOrderDetailViewModel
    {
        public long Id { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public int TotalQuantity { get; set; }
        public bool IsSentToQueue { get; set; }
        public DateTime? SentToQueueAt { get; set; }
        public AdminShippingAddressViewModel? ShippingAddress { get; set; }
        public string? Notes { get; set; }
        public List<AdminOrderItemViewModel> Items { get; set; } = new();
    }

    public class AdminShippingAddressViewModel
    {
        public string? ShippingAddress { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingPostalCode { get; set; }
        public string? ShippingCountry { get; set; }
    }

    public class AdminOrderItemViewModel
    {
        public long Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public int Size { get; set; }
        public string Color { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal PriceAtOrder { get; set; }
        public decimal SubTotal { get; set; }
        public AdminCurrentProductViewModel? CurrentProduct { get; set; }
    }

    public class AdminCurrentProductViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public bool PriceChanged { get; set; }
    }
}

