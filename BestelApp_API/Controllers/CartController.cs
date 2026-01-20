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
        public async Task<ActionResult<Cart>> GetCart()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            // Zoek of maak cart aan
            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.ShoeVariant)
                        .ThenInclude(sv => sv.Shoe)
                            .ThenInclude(s => s.Category)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                // Maak nieuwe lege cart aan
                cart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return Ok(cart);
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
