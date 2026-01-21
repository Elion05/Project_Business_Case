using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BestelApp_Models;
using BestelApp_API.Services;
using System.Security.Claims;
using System.Text.Json;

namespace BestelApp_API.Controllers
{
    /// <summary>
    /// Checkout controller voor cart → order conversie
    /// Bevat alle validaties voor veilige checkout
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Alleen ingelogde users kunnen checkout doen
    public class CheckoutController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly RabbitMQService _rabbitMQService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            ApplicationDbContext context,
            RabbitMQService rabbitMQService,
            ILogger<CheckoutController> logger)
        {
            _context = context;
            _rabbitMQService = rabbitMQService;
            _logger = logger;
        }

        /// <summary>
        /// Valideer cart voor checkout (zonder order te maken)
        /// GET: api/checkout/validate
        /// </summary>
        [HttpGet("validate")]
        public async Task<ActionResult<CheckoutValidationResult>> ValidateCheckout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            var validationResult = await ValidateCartForCheckout(userId);
            return Ok(validationResult);
        }

        /// <summary>
        /// Voer checkout uit: cart → order
        /// POST: api/checkout
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<OrderCheckoutResponse>> Checkout([FromBody] CheckoutRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            // Validatie 1: Check of user bestaat
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogError($"Checkout gefaald: User {userId} niet gevonden");
                return BadRequest(new { message = "User niet gevonden", blocked = true });
            }

            // Validatie 2: Check of user adres heeft
            if (string.IsNullOrEmpty(user.Address) || string.IsNullOrEmpty(user.City))
            {
                _logger.LogWarning($"Checkout geblokkeerd: User {userId} heeft geen adres");
                return BadRequest(new { message = "Adres ontbreekt. Vul je profiel aan.", blocked = true });
            }

            // Haal cart op met alle items
            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.ShoeVariant)
                        .ThenInclude(sv => sv.Shoe)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null || !cart.Items.Any())
            {
                _logger.LogWarning($"Checkout geblokkeerd: Lege cart voor user {userId}");
                return BadRequest(new { message = "Cart is leeg", blocked = true });
            }

            // Validatie 3: Valideer cart (stock, prijzen, etc)
            var validationResult = await ValidateCartForCheckout(userId);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning($"Checkout geblokkeerd voor user {userId}: {JsonSerializer.Serialize(validationResult.Errors)}");
                return BadRequest(new
                {
                    message = "Checkout geblokkeerd door validatie fouten",
                    blocked = true,
                    errors = validationResult.Errors
                });
            }

            // Begin database transactie
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Maak Order aan
                var order = new Order
                {
                    OrderId = GenerateOrderId(),
                    UserId = userId,
                    TotalPrice = cart.TotalPrice,
                    Status = "Pending",
                    OrderDate = DateTime.UtcNow,
                    ShippingAddress = user.Address,
                    ShippingCity = user.City,
                    ShippingPostalCode = user.PostalCode,
                    ShippingCountry = user.Country,
                    Notes = request.Notes ?? string.Empty
                };

                // Link User object voor RabbitMQ serialisatie
                order.User = user;

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // Save om Order.Id te krijgen

                // Converteer CartItems naar OrderItems
                foreach (var cartItem in cart.Items)
                {
                    var orderItem = new OrderItem
                    {
                        OrderId = order.OrderId, // Gebruik OrderId (string) ipv Id (long)
                        ShoeVariantId = cartItem.ShoeVariantId,
                        Quantity = cartItem.Quantity,
                        PriceAtOrder = cartItem.Price,
                        ProductName = cartItem.ShoeVariant.Shoe.Name,
                        Brand = cartItem.ShoeVariant.Shoe.Brand,
                        Size = cartItem.ShoeVariant.Size,
                        Color = cartItem.ShoeVariant.Color
                    };

                    _context.OrderItems.Add(orderItem);

                    // Update stock (belangrijke validatie!)
                    var shoeVariant = await _context.ShoeVariants.FindAsync(cartItem.ShoeVariantId);
                    if (shoeVariant == null || shoeVariant.Stock < cartItem.Quantity)
                    {
                        throw new Exception($"Stock onvoldoende voor {cartItem.ShoeVariant.Shoe.Name} (maat {cartItem.ShoeVariant.Size})");
                    }

                    shoeVariant.Stock -= cartItem.Quantity;
                    _logger.LogInformation($"Stock update: {shoeVariant.Shoe?.Brand} {shoeVariant.Shoe?.Name} maat {shoeVariant.Size} - {shoeVariant.Stock + cartItem.Quantity} → {shoeVariant.Stock}");
                }

                // Leeg cart
                _context.CartItems.RemoveRange(cart.Items);

                // Save alles
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Order {order.OrderId} succesvol aangemaakt voor user {userId}");

                // Laad order opnieuw met alle relaties voor RabbitMQ serialisatie
                var orderForRabbitMQ = await _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.OrderId == order.OrderId);

                if (orderForRabbitMQ == null)
                {
                    _logger.LogError($"Order {order.OrderId} niet gevonden na aanmaken!");
                    throw new Exception("Order niet gevonden na aanmaken");
                }

                // Stuur automatisch naar RabbitMQ
                try
                {
                    _logger.LogInformation($"Versturen order {orderForRabbitMQ.OrderId} naar RabbitMQ...");
                    await _rabbitMQService.SendOrderMessageAsync(orderForRabbitMQ);
                    
                    orderForRabbitMQ.IsSentToQueue = true;
                    orderForRabbitMQ.SentToQueueAt = DateTime.UtcNow;
                    orderForRabbitMQ.Status = "Processing"; // Update status naar Processing
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation($"✓ Order {orderForRabbitMQ.OrderId} succesvol verstuurd naar RabbitMQ");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"✗ Fout bij versturen order {orderForRabbitMQ?.OrderId ?? order.OrderId} naar RabbitMQ");
                    // Order is wel aangemaakt, maar niet verstuurd
                    // Status blijft "Pending" zodat admin handmatig kan proberen
                }

                return Ok(new OrderCheckoutResponse
                {
                    Success = true,
                    OrderId = order.OrderId,
                    OrderDatabaseId = order.Id,
                    TotalPrice = order.TotalPrice,
                    ItemCount = orderForRabbitMQ?.Items.Count ?? 0,
                    Message = "Bestelling succesvol geplaatst!"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Checkout gefaald voor user {userId}");
                return StatusCode(500, new
                {
                    message = "Checkout gefaald",
                    blocked = true,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Valideer cart voor checkout
        /// Controleert: stock, prijzen, user
        /// </summary>
        private async Task<CheckoutValidationResult> ValidateCartForCheckout(string userId)
        {
            var result = new CheckoutValidationResult { IsValid = true };

            // Check 1: User bestaat
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Field = "User",
                    Message = "User niet gevonden"
                });
                return result; // Stop hier, geen zin om verder te valideren
            }

            // Check 2: User heeft adres
            if (string.IsNullOrEmpty(user.Address) || string.IsNullOrEmpty(user.City))
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Field = "Address",
                    Message = "Adres ontbreekt. Vul je profiel aan."
                });
            }

            // Haal cart op
            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.ShoeVariant)
                        .ThenInclude(sv => sv.Shoe)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null || !cart.Items.Any())
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Field = "Cart",
                    Message = "Cart is leeg"
                });
                return result;
            }

            // Check 3: Valideer elk item
            foreach (var item in cart.Items)
            {
                // Check 3a: ShoeVariant bestaat nog
                var shoeVariant = await _context.ShoeVariants
                    .Include(sv => sv.Shoe)
                    .FirstOrDefaultAsync(sv => sv.Id == item.ShoeVariantId);

                if (shoeVariant == null)
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Field = $"Item-{item.Id}",
                        Message = "Product niet meer beschikbaar"
                    });
                    continue;
                }

                // Check 3b: Stock > 0 en voldoende
                if (shoeVariant.Stock <= 0)
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Field = $"Item-{item.Id}",
                        Message = $"{shoeVariant.Shoe.Brand} {shoeVariant.Shoe.Name} (maat {shoeVariant.Size}) is niet op voorraad",
                        ProductName = $"{shoeVariant.Shoe.Brand} {shoeVariant.Shoe.Name}",
                        Size = shoeVariant.Size
                    });
                }
                else if (shoeVariant.Stock < item.Quantity)
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Field = $"Item-{item.Id}",
                        Message = $"Niet genoeg stock voor {shoeVariant.Shoe.Brand} {shoeVariant.Shoe.Name} (maat {shoeVariant.Size}). Beschikbaar: {shoeVariant.Stock}, gevraagd: {item.Quantity}",
                        ProductName = $"{shoeVariant.Shoe.Brand} {shoeVariant.Shoe.Name}",
                        Size = shoeVariant.Size,
                        AvailableStock = shoeVariant.Stock,
                        RequestedQuantity = item.Quantity
                    });
                }

                // Check 3c: Prijs klopt (niet gewijzigd sinds toevoegen aan cart)
                if (Math.Abs(item.Price - shoeVariant.Shoe.Price) > 0.01m) // Kleine marge voor floating point
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Field = $"Item-{item.Id}",
                        Message = $"Prijs van {shoeVariant.Shoe.Brand} {shoeVariant.Shoe.Name} is gewijzigd. Was: €{item.Price}, nu: €{shoeVariant.Shoe.Price}",
                        ProductName = $"{shoeVariant.Shoe.Brand} {shoeVariant.Shoe.Name}",
                        OldPrice = item.Price,
                        NewPrice = shoeVariant.Shoe.Price
                    });
                }

                // Check 3d: Product is actief
                if (!shoeVariant.Shoe.IsActive)
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Field = $"Item-{item.Id}",
                        Message = $"{shoeVariant.Shoe.Brand} {shoeVariant.Shoe.Name} is niet meer beschikbaar",
                        ProductName = $"{shoeVariant.Shoe.Brand} {shoeVariant.Shoe.Name}"
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Genereer unieke Order ID
        /// </summary>
        private string GenerateOrderId()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"ORDER-{timestamp}-{random}";
        }
    }

    // Request/Response models
    public class CheckoutRequest
    {
        public string? Notes { get; set; }
    }

    public class OrderCheckoutResponse
    {
        public bool Success { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public long OrderDatabaseId { get; set; }
        public decimal TotalPrice { get; set; }
        public int ItemCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CheckoutValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();
    }

    public class ValidationError
    {
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public int? Size { get; set; }
        public int? AvailableStock { get; set; }
        public int? RequestedQuantity { get; set; }
        public decimal? OldPrice { get; set; }
        public decimal? NewPrice { get; set; }
    }
}
