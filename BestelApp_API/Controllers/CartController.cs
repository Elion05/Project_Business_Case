using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BestelApp_Models;
using System.Security.Claims;

namespace BestelApp_API.Controllers
{
    /// <summary>
    /// Cart controller voor shopping cart beheer
    /// Elke user heeft zijn eigen cart
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Alle cart acties vereisen ingelogde user
    public class CartController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CartController> _logger;

        public CartController(ApplicationDbContext context, ILogger<CartController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Haal huidige cart op voor ingelogde user
        /// GET: api/cart
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> GetCart()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            // Check of user daadwerkelijk bestaat (voorkomt FK errors bij oude tokens)
            if (!await _context.AppUsers.AnyAsync(u => u.Id == userId))
            {
                return Unauthorized(new { message = "User niet gevonden. Log opnieuw in." });
            }

            // BELANGRIJK:
            // We retourneren hier GEEN EF entities, omdat die navigation properties object-cycles kunnen maken:
            // Cart -> Items -> Cart -> Items -> ...
            // Daarom maken we een simpele DTO (anoniem object) zonder back-references.

            // Zorg dat er altijd een cart bestaat
            var cartBestaat = await _context.Carts.AnyAsync(c => c.UserId == userId);
            if (!cartBestaat)
            {
                var nieuweCart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(nieuweCart);
                await _context.SaveChangesAsync();
            }

            var cartDto = await _context.Carts
                .Where(c => c.UserId == userId)
                .Select(c => new
                {
                    c.Id,
                    c.UserId,
                    c.CreatedAt,
                    c.UpdatedAt,
                    Items = c.Items.Select(i => new
                    {
                        i.Id,
                        i.CartId,
                        i.ShoeVariantId,
                        i.Quantity,
                        i.Price,
                        i.AddedAt,
                        ShoeVariant = new
                        {
                            i.ShoeVariant.Id,
                            i.ShoeVariant.ShoeId,
                            i.ShoeVariant.Size,
                            i.ShoeVariant.Color,
                            i.ShoeVariant.Stock,
                            Shoe = new
                            {
                                i.ShoeVariant.Shoe.Id,
                                i.ShoeVariant.Shoe.Name,
                                i.ShoeVariant.Shoe.Brand,
                                i.ShoeVariant.Shoe.Price,
                                i.ShoeVariant.Shoe.ImageUrl,
                                Category = new
                                {
                                    i.ShoeVariant.Shoe.Category.Id,
                                    i.ShoeVariant.Shoe.Category.Name
                                }
                            }
                        }
                    }).ToList(),
                    TotalItems = c.Items.Sum(x => x.Quantity),
                    TotalPrice = c.Items.Sum(x => x.Quantity * x.Price)
                })
                .FirstOrDefaultAsync();

            if (cartDto == null)
            {
                // Heel uitzonderlijk, maar dan sturen we een lege cart terug
                return Ok(new
                {
                    Id = 0L,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Items = new List<object>(),
                    TotalItems = 0,
                    TotalPrice = 0m
                });
            }

            return Ok(cartDto);
        }

        /// <summary>
        /// Voeg item toe aan cart
        /// POST: api/cart/items
        /// </summary>
        [HttpPost("items")]
        public async Task<ActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            // Check of user daadwerkelijk bestaat
            if (!await _context.AppUsers.AnyAsync(u => u.Id == userId))
            {
                return Unauthorized(new { message = "User niet gevonden. Log opnieuw in." });
            }

            // Valideer ShoeVariant
            var shoeVariant = await _context.ShoeVariants
                .Include(sv => sv.Shoe)
                .FirstOrDefaultAsync(sv => sv.Id == request.ShoeVariantId);

            if (shoeVariant == null)
            {
                return NotFound(new { message = "ShoeVariant niet gevonden" });
            }

            // Check stock
            if (shoeVariant.Stock < request.Quantity)
            {
                return BadRequest(new { message = $"Niet genoeg stock. Beschikbaar: {shoeVariant.Stock}" });
            }

            // Haal of maak cart
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
            }

            // Check of item al in cart zit
            var existingItem = cart.Items.FirstOrDefault(i => i.ShoeVariantId == request.ShoeVariantId);
            if (existingItem != null)
            {
                // Update aantal
                existingItem.Quantity += request.Quantity;

                // Check stock weer
                if (existingItem.Quantity > shoeVariant.Stock)
                {
                    return BadRequest(new { message = $"Niet genoeg stock. Beschikbaar: {shoeVariant.Stock}" });
                }
            }
            else
            {
                // Voeg nieuw item toe
                var cartItem = new CartItem
                {
                    CartId = cart.Id,
                    ShoeVariantId = request.ShoeVariantId,
                    Quantity = request.Quantity,
                    Price = shoeVariant.Shoe.Price,
                    AddedAt = DateTime.UtcNow
                };
                cart.Items.Add(cartItem);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Item toegevoegd aan cart voor user {userId}: {shoeVariant.Shoe.Brand} {shoeVariant.Shoe.Name}");

            return Ok(new { message = "Item toegevoegd aan cart", cartItemCount = cart.Items.Count });
        }

        /// <summary>
        /// Update aantal van cart item
        /// PUT: api/cart/items/{itemId}
        /// </summary>
        [HttpPut("items/{itemId}")]
        public async Task<ActionResult> UpdateCartItem(long itemId, [FromBody] UpdateCartItemRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            // Zoek cart item en valideer ownership
            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .Include(ci => ci.ShoeVariant)
                    .ThenInclude(sv => sv.Shoe)
                .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.UserId == userId);

            if (cartItem == null)
            {
                return NotFound(new { message = "Cart item niet gevonden" });
            }

            // Check stock
            if (request.Quantity > cartItem.ShoeVariant.Stock)
            {
                return BadRequest(new { message = $"Niet genoeg stock. Beschikbaar: {cartItem.ShoeVariant.Stock}" });
            }

            // Update aantal
            cartItem.Quantity = request.Quantity;
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Cart item {itemId} updated voor user {userId}");

            return Ok(new { message = "Cart item geüpdatet" });
        }

        /// <summary>
        /// Verwijder item uit cart
        /// DELETE: api/cart/items/{itemId}
        /// </summary>
        [HttpDelete("items/{itemId}")]
        public async Task<ActionResult> RemoveFromCart(long itemId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            // Zoek cart item en valideer ownership
            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.UserId == userId);

            if (cartItem == null)
            {
                return NotFound(new { message = "Cart item niet gevonden" });
            }

            // Verwijder item
            _context.CartItems.Remove(cartItem);
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Cart item {itemId} verwijderd voor user {userId}");

            return Ok(new { message = "Item verwijderd uit cart" });
        }

        /// <summary>
        /// Leeg hele cart
        /// DELETE: api/cart
        /// </summary>
        [HttpDelete]
        public async Task<ActionResult> ClearCart()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                return NotFound(new { message = "Cart niet gevonden" });
            }

            // Verwijder alle items
            _context.CartItems.RemoveRange(cart.Items);
            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Cart geleegd voor user {userId}");

            return Ok(new { message = "Cart geleegd" });
        }
    }

    // Request models
    public class AddToCartRequest
    {
        public long ShoeVariantId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class UpdateCartItemRequest
    {
        public int Quantity { get; set; }
    }
}
