using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BestelApp_Models;
using System.Security.Claims;

namespace BestelApp_API.Controllers
{
    /// <summary>
    /// Favorites controller voor favorieten beheer
    /// Alleen ingelogde users kunnen favorieten gebruiken
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Alleen ingelogde users
    public class FavoritesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FavoritesController> _logger;

        public FavoritesController(
            ApplicationDbContext context,
            ILogger<FavoritesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// GET api/favorites - Haal alle favorieten van ingelogde user op
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> GetMyFavorites()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            try
            {
                var favorites = await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Include(f => f.Shoe)
                        .ThenInclude(s => s.Category)
                    .Include(f => f.Shoe)
                        .ThenInclude(s => s.Variants)
                    .Select(f => new
                    {
                        f.Id,
                        f.ShoeId,
                        f.AddedAt,
                        Shoe = new
                        {
                            f.Shoe.Id,
                            f.Shoe.Name,
                            f.Shoe.Brand,
                            f.Shoe.Price,
                            f.Shoe.ImageUrl,
                            f.Shoe.Gender,
                            f.Shoe.Description,
                            Category = f.Shoe.Category.Name,
                            Variants = f.Shoe.Variants.Select(v => new
                            {
                                v.Id,
                                v.Size,
                                v.Color,
                                v.Stock,
                                v.IsAvailable
                            }).ToList()
                        }
                    })
                    .OrderByDescending(f => f.AddedAt)
                    .ToListAsync();

                return Ok(favorites);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen favorieten voor user {UserId}", userId);
                return StatusCode(500, new { message = "Er ging iets fout bij het ophalen van favorieten" });
            }
        }

        /// <summary>
        /// POST api/favorites/{shoeId} - Voeg product toe aan favorieten
        /// </summary>
        [HttpPost("{shoeId}")]
        public async Task<ActionResult> AddToFavorites(long shoeId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            try
            {
                // Check of product bestaat
                var shoe = await _context.Shoes.FindAsync(shoeId);
                if (shoe == null)
                {
                    return NotFound(new { message = $"Product met ID {shoeId} niet gevonden" });
                }

                // Check of al favoriet is
                var exists = await _context.Favorites
                    .AnyAsync(f => f.UserId == userId && f.ShoeId == shoeId);

                if (exists)
                {
                    return BadRequest(new { message = "Product is al favoriet" });
                }

                // Voeg toe
                var favorite = new Favorite
                {
                    UserId = userId,
                    ShoeId = shoeId,
                    AddedAt = DateTime.UtcNow
                };

                _context.Favorites.Add(favorite);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Product {ShoeId} toegevoegd aan favorieten voor user {UserId}", shoeId, userId);

                return Ok(new
                {
                    message = "Product toegevoegd aan favorieten",
                    favoriteId = favorite.Id,
                    shoeId = shoeId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij toevoegen favoriet voor user {UserId}", userId);
                return StatusCode(500, new { message = "Er ging iets fout bij het toevoegen van favoriet" });
            }
        }

        /// <summary>
        /// DELETE api/favorites/{shoeId} - Verwijder uit favorieten
        /// </summary>
        [HttpDelete("{shoeId}")]
        public async Task<ActionResult> RemoveFromFavorites(long shoeId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            try
            {
                var favorite = await _context.Favorites
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.ShoeId == shoeId);

                if (favorite == null)
                {
                    return NotFound(new { message = "Product niet gevonden in favorieten" });
                }

                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Product {ShoeId} verwijderd uit favorieten voor user {UserId}", shoeId, userId);

                return Ok(new { message = "Product verwijderd uit favorieten" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij verwijderen favoriet voor user {UserId}", userId);
                return StatusCode(500, new { message = "Er ging iets fout bij het verwijderen van favoriet" });
            }
        }

        /// <summary>
        /// GET api/favorites/check/{shoeId} - Check of product favoriet is
        /// </summary>
        [HttpGet("check/{shoeId}")]
        public async Task<ActionResult> IsFavorite(long shoeId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Ok(new { isFavorite = false });
            }

            try
            {
                var isFavorite = await _context.Favorites
                    .AnyAsync(f => f.UserId == userId && f.ShoeId == shoeId);

                return Ok(new { isFavorite });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij checken favoriet status");
                return Ok(new { isFavorite = false });
            }
        }
    }
}
