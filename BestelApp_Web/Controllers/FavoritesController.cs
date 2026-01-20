using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BestelApp_Web.Services;

namespace BestelApp_Web.Controllers
{
    /// <summary>
    /// Controller voor favorieten beheer
    /// Alleen ingelogde users kunnen favorieten gebruiken
    /// </summary>
    [Authorize]
    public class FavoritesController : Controller
    {
        private readonly FavoritesApiService _favoritesApiService;
        private readonly ILogger<FavoritesController> _logger;

        public FavoritesController(
            FavoritesApiService favoritesApiService,
            ILogger<FavoritesController> logger)
        {
            _favoritesApiService = favoritesApiService;
            _logger = logger;
        }

        /// <summary>
        /// GET: Favorites/Index - Toon alle favorieten
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var favorites = await _favoritesApiService.GetFavoritesAsync();
                return View(favorites ?? new List<FavoriteResponse>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen favorieten");
                TempData["FoutBericht"] = "Er ging iets fout bij het ophalen van favorieten";
                return View(new List<FavoriteResponse>());
            }
        }

        /// <summary>
        /// POST: Favorites/Add/{shoeId} - Voeg product toe aan favorieten
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Add(long shoeId)
        {
            try
            {
                var success = await _favoritesApiService.AddToFavoritesAsync(shoeId);
                if (success)
                {
                    TempData["SuccessBericht"] = "Product toegevoegd aan favorieten";
                }
                else
                {
                    TempData["FoutBericht"] = "Fout bij toevoegen aan favorieten";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij toevoegen favoriet");
                TempData["FoutBericht"] = "Er ging iets fout";
            }

            return RedirectToAction("Index", "Shoes");
        }

        /// <summary>
        /// POST: Favorites/Remove/{shoeId} - Verwijder uit favorieten
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Remove(long shoeId)
        {
            try
            {
                var success = await _favoritesApiService.RemoveFromFavoritesAsync(shoeId);
                if (success)
                {
                    TempData["SuccessBericht"] = "Product verwijderd uit favorieten";
                }
                else
                {
                    TempData["FoutBericht"] = "Fout bij verwijderen uit favorieten";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij verwijderen favoriet");
                TempData["FoutBericht"] = "Er ging iets fout";
            }

            return RedirectToAction("Index");
        }
    }
}
