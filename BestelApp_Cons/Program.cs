using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;


var factory = new ConnectionFactory()
{
    HostName = "10.2.160.221",
    UserName = "user",
    Password = "user123!"
};

using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

await channel.QueueDeclareAsync(
    //de naam van de queue
    queue: "message",
    //messages blijven bewaard ook als de broker opnieuw opstart, als het False is en de app crasht zijn de messages weg
    durable: true,
    //als de subscriber weg is, wordt de queue verwijderd
    exclusive: false,
    autoDelete: false,
    arguments: null);


Console.WriteLine("Waiting for messages...");

var consumer = new AsyncEventingBasicConsumer(channel);
//messages krijgen van de queue
consumer.ReceivedAsync += async (sender, eventArgs) =>
{
    byte[] body = eventArgs.Body.ToArray();
    //omzetten van byte array naar string
    string message = Encoding.UTF8.GetString(body);

    Console.WriteLine($"Received: {message}");

    await ((AsyncEventingBasicConsumer)sender).Channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
};

await channel.BasicConsumeAsync(
    "message",
    //manueel de berichten bevestigen
    autoAck: false,
    consumer);


Console.ReadLine();