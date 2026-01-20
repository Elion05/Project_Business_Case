using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using BestelApp_Models;
using BestelApp_Web.Models;
using System.Text.Json;

namespace BestelApp_Web.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProfileController> _logger;
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProfileController(
            UserManager<Users> userManager,
            IConfiguration configuration,
            ILogger<ProfileController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            
            _httpClient = new HttpClient();
            var apiBaseUrl = _configuration["BackendApi:BaseUrl"] ?? "https://localhost:7001";
            _httpClient.BaseAddress = new Uri(apiBaseUrl);
        }

        /// <summary>
        /// GET: Profile/Index - Toon gebruikersprofiel
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new ProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Address = user.Address,
                City = user.City,
                PostalCode = user.PostalCode,
                Country = user.Country
            };

            return View(model);
        }

        /// <summary>
        /// POST: Profile/UpdateProfile - Update gebruikersgegevens
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Update gebruikersgegevens
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;
            user.Address = model.Address;
            user.City = model.City;
            user.PostalCode = model.PostalCode;
            user.Country = model.Country;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessBericht"] = "Profiel succesvol bijgewerkt";
            }
            else
            {
                TempData["FoutBericht"] = "Er ging iets fout bij het bijwerken van je profiel";
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// GET: Profile/Orders - Toon bestellingen
        /// </summary>
        public async Task<IActionResult> Orders()
        {
            try
            {
                // Forward cookies voor authenticatie
                ForwardCookies();

                var response = await _httpClient.GetAsync("/api/orders");
                
                if (response.IsSuccessStatusCode)
                {
                    var orders = await response.Content.ReadFromJsonAsync<List<OrderViewModel>>();
                    return View(orders ?? new List<OrderViewModel>());
                }
                else
                {
                    _logger.LogError($"Failed to get orders: {response.StatusCode}");
                    return View(new List<OrderViewModel>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen bestellingen");
                return View(new List<OrderViewModel>());
            }
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
