using RabbitMQ.Client;
using System.Text;
using BestelApp_Models;

namespace BestelApp_Web.Services
{
    public class RabbitMQService
    {
        private readonly ConnectionFactory _factory;

        public RabbitMQService()
        {
            _factory = new ConnectionFactory()
            {
                HostName = "10.2.160.221",
                UserName = "user",
                Password = "user123!"
            };
        }

        public async Task SendOrderMessageAsync(Shoe shoe)
        {
            using var connection = await _factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            var queueArgs = new Dictionary<string, object?>
            {
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", "BestelAppQueue_DLQ" }
            };

            await channel.QueueDeclareAsync(
                //dit is de queue naam, die kan maken in RabbitMQ om te gebruiken
                queue: "BestelAppQueue",
                //messages blijven bewaard ook als de broker opnieuw opstart, als het False is en de app crasht zijn de messages weg
                durable: true,
                //als de subscriber weg is, wordt de queue verwijderd
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs
            );

            // Maak een JSON bericht aan
            var bestellingBericht = new
            {
                orderId = $"ORDER-{DateTime.UtcNow:yyyyMMddHHmmss}-{shoe.Id}",
                brand = shoe.Brand,
                name = shoe.Name,
                size = shoe.Size,
                price = shoe.Price,
                createdAt = DateTime.UtcNow
            };

            // Converteer naar JSON string
            var jsonBericht = System.Text.Json.JsonSerializer.Serialize(bestellingBericht);
            
            // Converteer JSON string naar bytes
            var body = Encoding.UTF8.GetBytes(jsonBericht);

            // Maak properties met metadata
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = bestellingBericht.orderId,
                CorrelationId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            //Dit is de message die wordt gestuurd naar de queue  
            await channel.BasicPublishAsync(
                //exchange is default
                exchange: string.Empty,
                routingKey: "BestelAppQueue",
                mandatory: true,
                basicProperties: properties,
                body: body
            );

            // Log voor debugging
            Console.WriteLine($"JSON bericht verzonden:");
            Console.WriteLine($"  Order ID: {bestellingBericht.orderId}");
            Console.WriteLine($"  Product: {shoe.Brand} {shoe.Name}");
            Console.WriteLine($"  JSON: {jsonBericht}");
        }
    }
}