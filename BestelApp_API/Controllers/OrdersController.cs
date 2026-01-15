using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BestelApp_Models;
using BestelApp_API.Services;
using System.Security.Claims;

namespace BestelApp_API.Controllers
{
    /// <summary>
    /// Orders Controller voor order beheer
    /// Bevat endpoints voor order lijst, detail, en RabbitMQ verzending
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Alle order acties vereisen ingelogde user
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly RabbitMQService _rabbitMQService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            ApplicationDbContext context,
            RabbitMQService rabbitMQService,
            ILogger<OrdersController> logger)
        {
            _context = context;
            _rabbitMQService = rabbitMQService;
            _logger = logger;
        }

        // ====================
        // GET ENDPOINTS
        // ====================

        /// <summary>
        /// GET api/orders
        /// Haal alle orders op voor ingelogde user
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> GetMyOrders()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            try
            {
                var orders = await _context.Orders
                    .Where(o => o.UserId == userId)
                    .Include(o => o.Items)
                        .ThenInclude(i => i.ShoeVariant)
                            .ThenInclude(sv => sv.Shoe)
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => new
                    {
                        o.Id,
                        o.OrderId,
                        o.OrderDate,
                        o.Status,
                        o.TotalPrice,
                        o.TotalQuantity,
                        o.IsSentToQueue,
                        ItemCount = o.Items.Count,
                        Items = o.Items.Select(i => new
                        {
                            i.ProductName,
                            i.Brand,
                            i.Size,
                            i.Color,
                            i.Quantity,
                            i.PriceAtOrder
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen orders voor user {UserId}", userId);
                return StatusCode(500, new { message = "Er ging iets fout bij het ophalen van orders" });
            }
        }

        /// <summary>
        /// GET api/orders/{id}
        /// Haal 1 order op (detail)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult> GetOrder(long id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            try
            {
                // Check of user admin is
                var isAdmin = User.IsInRole("Admin");

                var order = await _context.Orders
                    .Where(o => o.Id == id && (o.UserId == userId || isAdmin))
                    .Include(o => o.User)
                    .Include(o => o.Items)
                        .ThenInclude(i => i.ShoeVariant)
                            .ThenInclude(sv => sv.Shoe)
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    return NotFound(new { message = $"Order met ID {id} niet gevonden" });
                }

                var result = new
                {
                    order.Id,
                    order.OrderId,
                    order.UserId,
                    UserName = order.User.UserName,
                    UserEmail = order.User.Email,
                    order.OrderDate,
                    order.Status,
                    order.TotalPrice,
                    order.TotalQuantity,
                    order.IsSentToQueue,
                    order.SentToQueueAt,
                    ShippingAddress = new
                    {
                        order.ShippingAddress,
                        order.ShippingCity,
                        order.ShippingPostalCode,
                        order.ShippingCountry
                    },
                    order.Notes,
                    Items = order.Items.Select(i => new
                    {
                        i.Id,
                        i.ProductName,
                        i.Brand,
                        i.Size,
                        i.Color,
                        i.Quantity,
                        i.PriceAtOrder,
                        i.SubTotal,
                        CurrentProduct = new
                        {
                            i.ShoeVariant.Shoe.Id,
                            i.ShoeVariant.Shoe.Name,
                            i.ShoeVariant.Shoe.Brand,
                            CurrentPrice = i.ShoeVariant.Shoe.Price,
                            PriceChanged = Math.Abs(i.PriceAtOrder - i.ShoeVariant.Shoe.Price) > 0.01m
                        }
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen order {OrderId}", id);
                return StatusCode(500, new { message = "Er ging iets fout" });
            }
        }

        /// <summary>
        /// GET api/orders/all
        /// Haal alle orders op (Admin only)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetAllOrders()
        {
            try
            {
                var orders = await _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.Items)
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => new
                    {
                        o.Id,
                        o.OrderId,
                        o.UserId,
                        UserName = o.User.UserName,
                        UserEmail = o.User.Email,
                        o.OrderDate,
                        o.Status,
                        o.TotalPrice,
                        o.TotalQuantity,
                        o.IsSentToQueue,
                        ItemCount = o.Items.Count
                    })
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen alle orders (admin)");
                return StatusCode(500, new { message = "Er ging iets fout" });
            }
        }

        // ====================
        // POST ENDPOINT - RABBITMQ
        // ====================

        /// <summary>
        /// POST api/orders/{id}/send
        /// Verstuur order naar RabbitMQ (na validatie)
        /// Alleen voor orders die nog niet verstuurd zijn
        /// </summary>
        [HttpPost("{id}/send")]
        public async Task<ActionResult> SendOrderToQueue(long id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User niet ingelogd" });
            }

            try
            {
                // Check of user admin is
                var isAdmin = User.IsInRole("Admin");

                // Haal order op met alle details
                var order = await _context.Orders
                    .Where(o => o.Id == id && (o.UserId == userId || isAdmin))
                    .Include(o => o.User)
                    .Include(o => o.Items)
                        .ThenInclude(i => i.ShoeVariant)
                            .ThenInclude(sv => sv.Shoe)
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    return NotFound(new { message = $"Order met ID {id} niet gevonden" });
                }

                // Validatie 1: Check of order al verstuurd is
                if (order.IsSentToQueue)
                {
                    return BadRequest(new
                    {
                        message = "Order is al verstuurd naar RabbitMQ",
                        sentAt = order.SentToQueueAt,
                        status = order.Status
                    });
                }

                // Validatie 2: Check of order status OK is
                if (order.Status == "Cancelled")
                {
                    return BadRequest(new { message = "Kan geannuleerde order niet versturen" });
                }

                // Validatie 3: Check of user bestaat
                if (order.User == null)
                {
                    _logger.LogError("Order {OrderId} heeft geen user!", order.OrderId);
                    return BadRequest(new { message = "User niet gevonden" });
                }

                // Validatie 4: Check of order items heeft
                if (!order.Items.Any())
                {
                    return BadRequest(new { message = "Order heeft geen items" });
                }

                // Validatie 5: Verifieer prijzen en stock (optioneel, maar veilig)
                var validationErrors = new List<string>();

                foreach (var item in order.Items)
                {
                    // Check of product nog bestaat
                    if (item.ShoeVariant == null || item.ShoeVariant.Shoe == null)
                    {
                        validationErrors.Add($"Product voor item {item.ProductName} niet meer beschikbaar");
                        continue;
                    }

                    // Check of prijs sterk is veranderd (waarschuwing, geen blokkade)
                    var priceDifference = Math.Abs(item.PriceAtOrder - item.ShoeVariant.Shoe.Price);
                    if (priceDifference > item.PriceAtOrder * 0.2m) // > 20% verschil
                    {
                        _logger.LogWarning("Prijs voor {ProductName} sterk veranderd: was €{OldPrice}, nu €{NewPrice}",
                            item.ProductName, item.PriceAtOrder, item.ShoeVariant.Shoe.Price);
                    }

                    // Check of product actief is
                    if (!item.ShoeVariant.Shoe.IsActive)
                    {
                        validationErrors.Add($"Product {item.ProductName} is niet meer actief");
                    }
                }

                // Als er validatie errors zijn, blokkeer dan
                if (validationErrors.Any())
                {
                    return BadRequest(new
                    {
                        message = "Order validatie gefaald",
                        errors = validationErrors
                    });
                }

                // Verstuur naar RabbitMQ
                try
                {
                    await _rabbitMQService.SendOrderMessageAsync(order);

                    // Update order status
                    order.IsSentToQueue = true;
                    order.SentToQueueAt = DateTime.UtcNow;
                    order.Status = "Processing";

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Order {OrderId} succesvol verstuurd naar RabbitMQ", order.OrderId);

                    return Ok(new
                    {
                        success = true,
                        message = "Order succesvol verstuurd naar RabbitMQ",
                        orderId = order.OrderId,
                        status = order.Status,
                        sentAt = order.SentToQueueAt,
                        details = new
                        {
                            itemCount = order.Items.Count,
                            totalPrice = order.TotalPrice,
                            totalQuantity = order.TotalQuantity
                        }
                    });
                }
                catch (Exception rabbitEx)
                {
                    _logger.LogError(rabbitEx, "Fout bij versturen order {OrderId} naar RabbitMQ", order.OrderId);

                    // Update status naar Failed
                    order.Status = "Failed";
                    await _context.SaveChangesAsync();

                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Fout bij versturen naar RabbitMQ",
                        error = rabbitEx.Message,
                        status = order.Status
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij verwerken order {OrderId}", id);
                return StatusCode(500, new { message = "Er ging iets fout bij het versturen van de order" });
            }
        }

        /// <summary>
        /// POST api/orders
        /// TIJDELIJK ENDPOINT: Accepteer basis order info en stuur direct naar RabbitMQ (voor testing)
        /// TODO: Vervang dit door volledige Cart → Checkout flow
        /// </summary>
        [HttpPost]
        [AllowAnonymous] // Tijdelijk geen auth voor testing
        public async Task<ActionResult> CreateQuickOrder([FromBody] QuickOrderRequest request)
        {
            try
            {
                _logger.LogInformation("Quick order ontvangen voor: {Brand} {Name}", request.Brand, request.Name);

                // Haal de eerste user op uit de database (voor tijdelijk testing)
                var firstUser = await _context.Users.FirstOrDefaultAsync();
                if (firstUser == null)
                {
                    return BadRequest(new { message = "Geen users gevonden in database. Run eerst de seeder!" });
                }

                // Maak een tijdelijke order
                var order = new Order
                {
                    OrderId = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    UserId = firstUser.Id, // Gebruik eerste user uit database
                    OrderDate = DateTime.UtcNow,
                    Status = "Pending",
                    TotalPrice = request.Price,
                    // TotalQuantity is computed property, niet handmatig zetten
                    ShippingAddress = "Test Address",
                    ShippingCity = "Test City",
                    ShippingPostalCode = "1000",
                    ShippingCountry = "Belgium",
                    Items = new List<OrderItem>
                    {
                        new OrderItem
                        {
                            ProductName = request.Name,
                            Brand = request.Brand,
                            Size = request.Size ?? 42, // Default maat als niet gezet
                            Color = request.Color ?? "Unknown", // Default kleur als niet gezet
                            Quantity = 1,
                            PriceAtOrder = request.Price
                        }
                    }
                };

                // Sla order op in database
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Verstuur naar RabbitMQ
                await _rabbitMQService.SendOrderMessageAsync(order);

                _logger.LogInformation("Quick order succesvol verwerkt: {OrderId}", order.OrderId);

                return Ok(new
                {
                    message = "Order succesvol geplaatst",
                    orderId = order.OrderId,
                    totalPrice = order.TotalPrice
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij verwerken quick order");
                return StatusCode(500, new { message = "Er ging iets fout bij het plaatsen van de order" });
            }
        }

        // ====================
        // ADMIN ENDPOINTS
        // ====================

        /// <summary>
        /// PUT api/orders/{id}/status
        /// Update order status (Admin only)
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> UpdateOrderStatus(long id, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                var order = await _context.Orders.FindAsync(id);
                if (order == null)
                {
                    return NotFound(new { message = $"Order met ID {id} niet gevonden" });
                }

                order.Status = request.Status;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Order {OrderId} status geüpdatet naar {Status} door admin", order.OrderId, request.Status);

                return Ok(new { message = "Status geüpdatet", orderId = order.OrderId, newStatus = order.Status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij updaten order status");
                return StatusCode(500, new { message = "Er ging iets fout" });
            }
        }

        /// <summary>
        /// GET api/orders/health
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "Orders API"
            });
        }
    }

    // Request DTOs
    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Simpel DTO voor tijdelijk quick order endpoint
    /// Vermijdt validatie problemen met volledig Shoe model
    /// </summary>
    public class QuickOrderRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int? Size { get; set; }
        public string? Color { get; set; }
    }
}
