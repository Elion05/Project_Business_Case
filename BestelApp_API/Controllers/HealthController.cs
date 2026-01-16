using Microsoft.AspNetCore.Mvc;
using BestelApp_API.Services;
using Microsoft.EntityFrameworkCore;
using BestelApp_Models;

namespace BestelApp_API.Controllers
{
    /// <summary>
    /// Health Check Controller
    /// Test connecties met database en RabbitMQ
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly RabbitMQService _rabbitMQService;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            ApplicationDbContext context,
            RabbitMQService rabbitMQService,
            ILogger<HealthController> logger)
        {
            _context = context;
            _rabbitMQService = rabbitMQService;
            _logger = logger;
        }

        /// <summary>
        /// GET api/health
        /// Basis health check
        /// </summary>
        [HttpGet]
        public IActionResult GetHealth()
        {
            return Ok(new
            {
                status = "healthy",
                service = "BestelApp API",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }

        /// <summary>
        /// GET api/health/detailed
        /// Gedetailleerde health check met database en RabbitMQ
        /// </summary>
        [HttpGet("detailed")]
        public async Task<IActionResult> GetDetailedHealth()
        {
            var healthChecks = new Dictionary<string, object>();

            // Check 1: API zelf
            healthChecks["api"] = new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow
            };

            // Check 2: Database
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                var userCount = await _context.Users.CountAsync();
                var orderCount = await _context.Orders.CountAsync();

                healthChecks["database"] = new
                {
                    status = canConnect ? "healthy" : "unhealthy",
                    canConnect = canConnect,
                    stats = new
                    {
                        users = userCount,
                        orders = orderCount
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check gefaald");
                healthChecks["database"] = new
                {
                    status = "unhealthy",
                    error = ex.Message
                };
            }

            // Check 3: RabbitMQ
            try
            {
                var rabbitMqHealthy = await _rabbitMQService.TestConnectionAsync();
                healthChecks["rabbitmq"] = new
                {
                    status = rabbitMqHealthy ? "healthy" : "unhealthy",
                    queue = "BestelAppQueue",
                    durable = true,
                    persistent = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ health check gefaald");
                healthChecks["rabbitmq"] = new
                {
                    status = "unhealthy",
                    error = ex.Message
                };
            }

            // Bepaal overall status
            var allHealthy = healthChecks.Values.All(v =>
            {
                var statusProp = v.GetType().GetProperty("status");
                return statusProp?.GetValue(v)?.ToString() == "healthy";
            });

            var response = new
            {
                status = allHealthy ? "healthy" : "degraded",
                timestamp = DateTime.UtcNow,
                checks = healthChecks
            };

            return allHealthy ? Ok(response) : StatusCode(503, response);
        }

        /// <summary>
        /// GET api/health/rabbitmq
        /// Test alleen RabbitMQ verbinding
        /// </summary>
        [HttpGet("rabbitmq")]
        public async Task<IActionResult> GetRabbitMqHealth()
        {
            try
            {
                var isHealthy = await _rabbitMQService.TestConnectionAsync();

                var response = new
                {
                    status = isHealthy ? "healthy" : "unhealthy",
                    service = "RabbitMQ",
                    timestamp = DateTime.UtcNow,
                    configuration = new
                    {
                        queue = "BestelAppQueue",
                        durable = true,
                        persistent = true,
                        contentType = "application/json"
                    }
                };

                return isHealthy ? Ok(response) : StatusCode(503, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ health check gefaald");
                return StatusCode(503, new
                {
                    status = "unhealthy",
                    service = "RabbitMQ",
                    timestamp = DateTime.UtcNow,
                    error = ex.Message
                });
            }
        }
    }
}
