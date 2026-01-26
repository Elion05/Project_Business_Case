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
        public async Task<IActionResult> Index(string? zoek, string? status, string? datumFilter, bool? queueStatus)
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

                // FILTERS TOEPASSEN
                var gefilterdeOrders = orders.AsEnumerable();

                // Zoekfilter (OrderId, klant naam, email)
                if (!string.IsNullOrWhiteSpace(zoek))
                {
                    var zoekLower = zoek.ToLower();
                    gefilterdeOrders = gefilterdeOrders.Where(o =>
                        o.OrderId.ToLower().Contains(zoekLower) ||
                        o.UserName.ToLower().Contains(zoekLower) ||
                        o.UserEmail.ToLower().Contains(zoekLower));
                }

                // Status filter
                if (!string.IsNullOrWhiteSpace(status) && status != "Alles")
                {
                    gefilterdeOrders = gefilterdeOrders.Where(o => 
                        o.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
                }

                // Datum filter
                if (!string.IsNullOrWhiteSpace(datumFilter))
                {
                    var vandaag = DateTime.Today;
                    gefilterdeOrders = datumFilter.ToLower() switch
                    {
                        "vandaag" => gefilterdeOrders.Where(o => o.OrderDate.Date == vandaag),
                        "deze week" => gefilterdeOrders.Where(o => o.OrderDate >= vandaag.AddDays(-(int)vandaag.DayOfWeek)),
                        "deze maand" => gefilterdeOrders.Where(o => o.OrderDate.Year == vandaag.Year && o.OrderDate.Month == vandaag.Month),
                        _ => gefilterdeOrders
                    };
                }

                // Queue status filter
                if (queueStatus.HasValue)
                {
                    gefilterdeOrders = gefilterdeOrders.Where(o => o.IsSentToQueue == queueStatus.Value);
                }

                // Sorteer op datum (nieuwste eerst)
                gefilterdeOrders = gefilterdeOrders.OrderByDescending(o => o.OrderDate);

                // ViewBag voor filters
                ViewBag.Zoek = zoek ?? string.Empty;
                ViewBag.Status = status ?? "Alles";
                ViewBag.DatumFilter = datumFilter ?? "Alles";
                ViewBag.QueueStatus = queueStatus;

                // Statistieken berekenen
                ViewBag.TotaalOrders = gefilterdeOrders.Count();
                ViewBag.TotaalOmzet = gefilterdeOrders.Sum(o => o.TotalPrice);
                ViewBag.TotaalItems = gefilterdeOrders.Sum(o => o.TotalQuantity);
                ViewBag.GemiddeldeOrderWaarde = ViewBag.TotaalOrders > 0 
                    ? Math.Round((decimal)ViewBag.TotaalOmzet / (int)ViewBag.TotaalOrders, 2) 
                    : 0;

                return View(gefilterdeOrders.ToList());
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

        // GET: AdminOrders/Export
        public async Task<IActionResult> Export(string? zoek, string? status, string? datumFilter, bool? queueStatus)
        {
            try
            {
                ForwardCookies();
                var response = await _httpClient.GetAsync("/api/orders/all");
                if (!response.IsSuccessStatusCode)
                {
                    TempData["FoutBericht"] = "Kon orders niet ophalen voor export.";
                    return RedirectToAction(nameof(Index));
                }

                var json = await response.Content.ReadAsStringAsync();
                var orders = JsonSerializer.Deserialize<List<AdminOrderListItemViewModel>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AdminOrderListItemViewModel>();

                // Pas dezelfde filters toe als Index
                var gefilterdeOrders = orders.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(zoek))
                {
                    var zoekLower = zoek.ToLower();
                    gefilterdeOrders = gefilterdeOrders.Where(o =>
                        o.OrderId.ToLower().Contains(zoekLower) ||
                        o.UserName.ToLower().Contains(zoekLower) ||
                        o.UserEmail.ToLower().Contains(zoekLower));
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "Alles")
                {
                    gefilterdeOrders = gefilterdeOrders.Where(o => 
                        o.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(datumFilter))
                {
                    var vandaag = DateTime.Today;
                    gefilterdeOrders = datumFilter.ToLower() switch
                    {
                        "vandaag" => gefilterdeOrders.Where(o => o.OrderDate.Date == vandaag),
                        "deze week" => gefilterdeOrders.Where(o => o.OrderDate >= vandaag.AddDays(-(int)vandaag.DayOfWeek)),
                        "deze maand" => gefilterdeOrders.Where(o => o.OrderDate.Year == vandaag.Year && o.OrderDate.Month == vandaag.Month),
                        _ => gefilterdeOrders
                    };
                }

                if (queueStatus.HasValue)
                {
                    gefilterdeOrders = gefilterdeOrders.Where(o => o.IsSentToQueue == queueStatus.Value);
                }

                gefilterdeOrders = gefilterdeOrders.OrderByDescending(o => o.OrderDate);

                // Maak CSV
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Order ID,Klant Naam,Klant Email,Datum,Status,Items,Totaal (€),Queue Status");

                foreach (var order in gefilterdeOrders)
                {
                    var statusText = order.Status.ToLower() switch
                    {
                        "pending" => "In behandeling",
                        "processing" => "Wordt verwerkt",
                        "shipped" => "Verzonden",
                        "delivered" => "Afgeleverd",
                        "completed" => "Voltooid",
                        "cancelled" => "Geannuleerd",
                        "failed" => "Mislukt",
                        _ => order.Status
                    };

                    csv.AppendLine($"{order.OrderId},{order.UserName},{order.UserEmail},{order.OrderDate:yyyy-MM-dd HH:mm},{statusText},{order.ItemCount},{order.TotalPrice:F2},{(order.IsSentToQueue ? "Verstuurd" : "Niet verstuurd")}");
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                var fileName = $"orders_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij exporteren orders");
                TempData["FoutBericht"] = "Er ging iets fout bij het exporteren.";
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

        // POST: AdminOrders/UpdateStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(long id, string status)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(status))
                {
                    TempData["FoutBericht"] = "Status is verplicht.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                ForwardCookies();
                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new { status }),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PutAsync($"/api/orders/{id}/status", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    TempData["SuccessBericht"] = $"Order status succesvol geüpdatet naar: {status}";
                    _logger.LogInformation("Order {OrderId} status geüpdatet naar {Status}", id, status);
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    TempData["FoutBericht"] = $"Kon status niet updaten. ({response.StatusCode})";
                    _logger.LogError("Order status update failed: {Status} - {Body}", response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij updaten order status");
                TempData["FoutBericht"] = "Er ging iets fout bij het updaten van de status.";
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

