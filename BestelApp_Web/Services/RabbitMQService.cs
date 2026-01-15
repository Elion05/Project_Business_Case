using System.Text;
using BestelApp_Models;
using BestelApp_Shared;
using RabbitMQ.Client;

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

            // GEBRUIK PASSIVE DECLARE OM ARGUMENT MISMATCH TE VOORKOMEN
            // De queue bestaat al (gemaakt via Management Console of elders) met specifieke settings (DLX).
            // We willen niet crashen omdat onze definitie afwijkt.
            try
            {
                await channel.QueueDeclarePassiveAsync("BestelAppQueue");
            }
            catch (Exception ex)
            {
                // Log de fout maar crash niet meteen hard, of throw een duidelijkere error
                Console.WriteLine($"[ERROR] Kan queue BestelAppQueue niet vinden of settings wijken af: {ex.Message}");
                throw;
            }

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

            // ENCRYPTIE
            var encryptedBericht = EncryptionHelper.Encrypt(jsonBericht);

            // LOGGING TOEVOEGEN VOOR VERIFICATIE
            Console.WriteLine($"[VERIFICATIE] Encrypted Message: {encryptedBericht}");

            // Converteer encrypted string naar bytes
            // We sturen nu de encrypted string over de lijn, niet de leesbare JSON
            var body = Encoding.UTF8.GetBytes(encryptedBericht);

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
        }
    }
}
