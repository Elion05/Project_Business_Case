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

            await channel.QueueDeclareAsync(
                //dit is de queue naam, die kan maken in RabbitMQ om te gebruiken
                queue: "BestelAppQueue",
                //messages blijven bewaard ook als de broker opnieuw opstart, als het False is en de app crasht zijn de messages weg
                durable: true,
                //als de subscriber weg is, wordt de queue verwijderd
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            //Dit is de message die je krijgt als je hebt besteld
            var message = $"Bestelling geplaatst: {shoe.Brand} {shoe.Name} (Maat: {shoe.Size}) - €{shoe.Price}";
            //De message omzetten naar bytes
            var body = Encoding.UTF8.GetBytes(message);

            //Dit is de message die wordt gestuurd naar de queue  
            await channel.BasicPublishAsync(
                //exchange is default
                exchange: string.Empty,
                routingKey: "BestelAppQueue", //Gebruik de queue naam als routing key bij default exchange
                mandatory: true,
                //Deze basicproperties zorgen ervoor dat de message persistent is
                basicProperties: new BasicProperties { Persistent = true },
                body: body
            );

            Console.WriteLine($"Sent: {message}");
        }
    }
}