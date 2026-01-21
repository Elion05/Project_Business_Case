using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BestelApp_Web.Services;

namespace BestelApp_Web.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly CartApiService _cartApiService;
        private readonly ILogger<CartController> _logger;

        public CartController(CartApiService cartApiService, ILogger<CartController> logger)
        {
            _cartApiService = cartApiService;
            _logger = logger;
        }

        /// <summary>
        /// GET: Cart/Index - Toon winkelwagen
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var cart = await _cartApiService.GetCartAsync();
            return View(cart);
        }

        /// <summary>
        /// POST: Cart/Add - Voeg product toe aan winkelwagen
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Add(long shoeVariantId, int quantity = 1, string? returnUrl = null)
        {
            try
            {
                var result = await _cartApiService.AddToCartAsync(shoeVariantId, quantity);
                if (result.Gelukt)
                {
                    // Trigger een subtiele animatie op de cart badge (in navbar) na toevoegen
                    TempData["CartPulse"] = "1";

                    // Als count 0 blijft, dan is er iets mis (debug)
                    TempData["SuccessBericht"] = result.CartItemCount > 0
                        ? $"Product toegevoegd aan winkelwagen (items: {result.CartItemCount})"
                        : "Product toegevoegd aan winkelwagen";
                }
                else
                {
                    TempData["FoutBericht"] = "Fout bij toevoegen aan winkelwagen";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij toevoegen aan cart");
                TempData["FoutBericht"] = "Er ging iets fout";
            }

            // Blijf op dezelfde pagina als er een returnUrl is (bv. Shoes/Details)
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// POST: Cart/Update - Update aantal van item
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Update(long itemId, int quantity)
        {
            try
            {
                var success = await _cartApiService.UpdateCartItemAsync(itemId, quantity);
                if (success)
                {
                    TempData["SuccessBericht"] = "Winkelwagen bijgewerkt";
                }
                else
                {
                    TempData["FoutBericht"] = "Fout bij bijwerken winkelwagen";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij updaten cart item");
                TempData["FoutBericht"] = "Er ging iets fout";
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// POST: Cart/Remove - Verwijder item uit winkelwagen
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Remove(long itemId)
        {
            try
            {
                var success = await _cartApiService.RemoveFromCartAsync(itemId);
                if (success)
                {
                    TempData["SuccessBericht"] = "Product verwijderd uit winkelwagen";
                }
                else
                {
                    TempData["FoutBericht"] = "Fout bij verwijderen uit winkelwagen";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij verwijderen uit cart");
                TempData["FoutBericht"] = "Er ging iets fout";
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// POST: Cart/Clear - Leeg hele winkelwagen
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Clear()
        {
            try
            {
                var success = await _cartApiService.ClearCartAsync();
                if (success)
                {
                    TempData["SuccessBericht"] = "Winkelwagen geleegd";
                }
                else
                {
                    TempData["FoutBericht"] = "Fout bij legen winkelwagen";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij legen cart");
                TempData["FoutBericht"] = "Er ging iets fout";
            }

            return RedirectToAction("Index");
        }
    }
}
