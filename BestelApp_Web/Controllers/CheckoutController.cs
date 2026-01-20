using System.Text;
using System.Text.Json;
using BestelApp_Models;
using BestelApp_Web.Models;
using BestelApp_Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BestelApp_Web.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly CartApiService _cartApiService;
        private readonly UserManager<Users> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CheckoutController> _logger;
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CheckoutController(
            CartApiService cartApiService,
            UserManager<Users> userManager,
            IConfiguration configuration,
            ILogger<CheckoutController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _cartApiService = cartApiService;
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;

            _httpClient = new HttpClient();
            var apiBaseUrl = _configuration["BackendApi:BaseUrl"] ?? "https://localhost:7001";
            _httpClient.BaseAddress = new Uri(apiBaseUrl);
        }

        /// <summary>
        /// GET: Checkout/Index - Toon checkout pagina
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // Haal cart op
            var cart = await _cartApiService.GetCartAsync();
            if (cart == null || cart.Items == null || !cart.Items.Any())
            {
                TempData["FoutBericht"] = "Je winkelwagen is leeg";
                return RedirectToAction("Index", "Cart");
            }

            // Haal gebruiker op voor adresgegevens
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new CheckoutViewModel
            {
                Cart = cart,
                Address = user.Address,
                City = user.City,
                PostalCode = user.PostalCode,
                Country = user.Country,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty
            };

            return View(model);
        }

        /// <summary>
        /// POST: Checkout/PlaceOrder - Plaats bestelling
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(CheckoutViewModel model)
        {
            // Valideer adresgegevens
            if (string.IsNullOrEmpty(model.Address) ||
                string.IsNullOrEmpty(model.City) ||
                string.IsNullOrEmpty(model.PostalCode) ||
                string.IsNullOrEmpty(model.Country))
            {
                TempData["FoutBericht"] = "Vul alle adresgegevens in";
                return RedirectToAction("Index");
            }

            try
            {
                // Update gebruiker adresgegevens indien nodig
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    bool addressChanged = false;
                    if (user.Address != model.Address) { user.Address = model.Address; addressChanged = true; }
                    if (user.City != model.City) { user.City = model.City; addressChanged = true; }
                    if (user.PostalCode != model.PostalCode) { user.PostalCode = model.PostalCode; addressChanged = true; }
                    if (user.Country != model.Country) { user.Country = model.Country; addressChanged = true; }

                    if (addressChanged)
                    {
                        await _userManager.UpdateAsync(user);
                    }
                }

                // Forward cookies voor authenticatie
                ForwardCookies();

                // Plaats bestelling via API
                var checkoutRequest = new
                {
                    notes = model.Notes ?? string.Empty
                };

                var json = JsonSerializer.Serialize(checkoutRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/checkout", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
                    TempData["SuccessBericht"] = $"Bestelling geplaatst! Order ID: {result?.OrderId}";
                    return RedirectToAction("OrderConfirmation", new { orderId = result?.OrderId });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Checkout failed: {errorContent}");
                    TempData["FoutBericht"] = "Er ging iets fout bij het plaatsen van de bestelling";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij plaatsen bestelling");
                TempData["FoutBericht"] = "Er ging iets fout bij het plaatsen van de bestelling";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// GET: Checkout/OrderConfirmation - Toon bevestiging
        /// </summary>
        public IActionResult OrderConfirmation(string orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
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
}
