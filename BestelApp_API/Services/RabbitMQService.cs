using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using BestelApp_Models;
using BestelApp_API.Models;

namespace BestelApp_API.Services
{
    /// <summary>
    /// RabbitMQ Service voor order verzending
    /// 
    /// Features:
    /// - Vaste order queue (BestelAppQueue)
    /// - Durable queue (blijft bestaan na restart)
    /// - Persistent messages (overleven broker restart)
    /// - JSON serialisatie
    /// - MessageId en CorrelationId voor tracking
    /// - Uitgebreide logging van elke message
    /// </summary>
    public class RabbitMQService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMQService> _logger;

        // Constanten voor queue configuratie
        private const string DEFAULT_HOSTNAME = "10.2.160.221";
        private const string DEFAULT_USERNAME = "user";
        private const string DEFAULT_PASSWORD = "user123!";
        private const string DEFAULT_QUEUE_NAME = "BestelAppQueue";

        // Queue configuratie
        private const bool QUEUE_DURABLE = true;        // Queue blijft bestaan na restart
        private const bool QUEUE_EXCLUSIVE = false;     // Niet exclusief voor 1 connectie
        private const bool QUEUE_AUTO_DELETE = false;   // Blijft bestaan als er geen consumers zijn

        // Message configuratie
        private const bool MESSAGE_PERSISTENT = true;   // Messages overleven broker restart
        private const string CONTENT_TYPE = "application/json";

        public RabbitMQService(IConfiguration configuration, ILogger<RabbitMQService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Verstuur Order naar RabbitMQ
        /// 
        /// Features:
        /// 1. Vaste order queue (BestelAppQueue)
        /// 2. Durable queue (blijft bestaan na restart)
        /// 3. Persistent messages (overleven broker restart)
        /// 4. JSON serialisatie met alle order details
        /// 5. MessageId voor unieke identificatie
        /// 6. CorrelationId voor request tracking
        /// 7. Uitgebreide logging van elke message
        /// 
        /// Alleen versturen na volledige validatie!
        /// </summary>
        public async Task SendOrderMessageAsync(Order order)
        {
            var correlationId = Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation("═══════════════════════════════════════");
                _logger.LogInformation("Start verzenden order naar RabbitMQ");
                _logger.LogInformation("Order ID: {OrderId}", order.OrderId);
                _logger.LogInformation("CorrelationId: {CorrelationId}", correlationId);

                // Lees configuratie
                var hostName = _configuration["RabbitMQ:HostName"] ?? DEFAULT_HOSTNAME;
                var userName = _configuration["RabbitMQ:UserName"] ?? DEFAULT_USERNAME;
                var password = _configuration["RabbitMQ:Password"] ?? DEFAULT_PASSWORD;
                var queueName = _configuration["RabbitMQ:QueueName"] ?? DEFAULT_QUEUE_NAME;

                _logger.LogInformation("RabbitMQ configuratie:");
                _logger.LogInformation("  Host: {HostName}", hostName);
                _logger.LogInformation("  Queue: {QueueName}", queueName);
                _logger.LogInformation("  Durable: {Durable}", QUEUE_DURABLE);
                _logger.LogInformation("  Persistent: {Persistent}", MESSAGE_PERSISTENT);

                // Maak ConnectionFactory aan
                var factory = new ConnectionFactory()
                {
                    HostName = hostName,
                    UserName = userName,
                    Password = password,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
                    SocketReadTimeout = TimeSpan.FromSeconds(30),
                    SocketWriteTimeout = TimeSpan.FromSeconds(30)
                };

                // Maak connectie en channel
                using var connection = await factory.CreateConnectionAsync();
                _logger.LogInformation("Connectie met RabbitMQ gemaakt");

                using var channel = await connection.CreateChannelAsync();
                _logger.LogInformation("Channel aangemaakt");

                // GEEN QueueDeclare - de Consumer maakt de queue aan met DLX configuratie
                // API stuurt alleen messages naar bestaande queue
                _logger.LogInformation("Gebruik bestaande queue: {QueueName}", queueName);

                // Maak OrderMessage DTO aan
                var orderMessage = new OrderMessage
                {
                    OrderId = order.OrderId,
                    UserId = order.UserId,
                    UserName = order.User?.UserName ?? "Onbekend",
                    UserEmail = order.User?.Email ?? "Onbekend",
                    Items = order.Items.Select(i => new OrderItemMessage
                    {
                        ProductName = i.ProductName,
                        Brand = i.Brand,
                        Size = i.Size,
                        Color = i.Color,
                        Quantity = i.Quantity,
                        Price = i.PriceAtOrder,
                        SubTotal = i.Quantity * i.PriceAtOrder
                    }).ToList(),
                    TotalPrice = order.TotalPrice,
                    TotalQuantity = order.TotalQuantity,
                    ShippingAddress = new ShippingAddressMessage
                    {
                        Address = order.ShippingAddress,
                        City = order.ShippingCity,
                        PostalCode = order.ShippingPostalCode,
                        Country = order.ShippingCountry,
                        FullAddress = $"{order.ShippingAddress}, {order.ShippingPostalCode} {order.ShippingCity}, {order.ShippingCountry}"
                    },
                    OrderDate = order.OrderDate,
                    Status = order.Status,
                    Notes = order.Notes,
                    MessageCreatedAt = DateTime.UtcNow
                };

                // Converteer naar JSON string (met pretty print voor logging)
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var jsonBericht = JsonSerializer.Serialize(orderMessage, jsonOptions);

                _logger.LogInformation("Order message JSON aangemaakt:");
                _logger.LogInformation("{Json}", jsonBericht);

                // Converteer naar bytes voor verzending
                var body = Encoding.UTF8.GetBytes(jsonBericht);

                // Maak PERSISTENT message properties met MessageId en CorrelationId
                var properties = new BasicProperties
                {
                    Persistent = MESSAGE_PERSISTENT,  // Message overleeft broker restart
                    ContentType = CONTENT_TYPE,       // application/json
                    MessageId = order.OrderId,        // Unieke message ID
                    CorrelationId = correlationId,    // Voor request tracking
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    AppId = "BestelApp_API",
                    UserId = userName,
                    Type = "OrderMessage",
                    DeliveryMode = DeliveryModes.Persistent  // Persistent delivery mode
                };

                _logger.LogInformation("Message properties:");
                _logger.LogInformation("  MessageId: {MessageId}", properties.MessageId);
                _logger.LogInformation("  CorrelationId: {CorrelationId}", properties.CorrelationId);
                _logger.LogInformation("  Persistent: {Persistent}", properties.Persistent);
                _logger.LogInformation("  ContentType: {ContentType}", properties.ContentType);
                _logger.LogInformation("  DeliveryMode: {DeliveryMode}", properties.DeliveryMode);

                // Verstuur message naar queue
                await channel.BasicPublishAsync(
                    exchange: string.Empty,      // Default exchange
                    routingKey: queueName,       // Queue name als routing key
                    mandatory: true,             // Return message als queue niet bestaat
                    basicProperties: properties,
                    body: body
                );

                _logger.LogInformation("✓ Message verzonden naar queue '{QueueName}'", queueName);
                _logger.LogInformation("Order details:");
                _logger.LogInformation("  User: {UserName} ({UserEmail})", orderMessage.UserName, orderMessage.UserEmail);
                _logger.LogInformation("  Items: {ItemCount}", orderMessage.Items.Count);
                _logger.LogInformation("  Totaal: €{TotalPrice}", orderMessage.TotalPrice);
                _logger.LogInformation("  Verzendadres: {Address}", orderMessage.ShippingAddress.FullAddress);
                _logger.LogInformation("═══════════════════════════════════════");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "✗ FOUT bij versturen naar RabbitMQ");
                _logger.LogError("Order ID: {OrderId}", order.OrderId);
                _logger.LogError("CorrelationId: {CorrelationId}", correlationId);
                _logger.LogError("═══════════════════════════════════════");
                throw;
            }
        }

        /// <summary>
        /// Test RabbitMQ verbinding
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var hostName = _configuration["RabbitMQ:HostName"] ?? DEFAULT_HOSTNAME;
                var userName = _configuration["RabbitMQ:UserName"] ?? DEFAULT_USERNAME;
                var password = _configuration["RabbitMQ:Password"] ?? DEFAULT_PASSWORD;

                var factory = new ConnectionFactory()
                {
                    HostName = hostName,
                    UserName = userName,
                    Password = password,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
                };

                using var connection = await factory.CreateConnectionAsync();
                using var channel = await connection.CreateChannelAsync();

                _logger.LogInformation("✓ RabbitMQ verbinding succesvol getest");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "✗ RabbitMQ verbinding test gefaald");
                return false;
            }
        }
    }
}
