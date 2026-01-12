using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using BestelApp_Cons.Models;
using BestelApp_Cons.Salesforce;

// ========================================
// STAP 1: Configuratie laden
// ========================================
Console.WriteLine("📋 Configuratie laden...");

var configuratie = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// ========================================
// STAP 2: Salesforce services initialiseren
// ========================================
Console.WriteLine("🔐 Salesforce services initialiseren...");

var salesforceAuthService = new SalesforceAuthService(configuratie);
var salesforceClient = new SalesforceClient(configuratie, salesforceAuthService);

// ========================================
// STAP 3: RabbitMQ connectie opzetten
// ========================================
Console.WriteLine("🐰 RabbitMQ connectie opzetten...");

var factory = new ConnectionFactory()
{
    HostName = configuratie["RabbitMQ:HostName"] ?? "10.2.160.221",
    UserName = configuratie["RabbitMQ:UserName"] ?? "user",
    Password = configuratie["RabbitMQ:Password"] ?? "user123!"
};

using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

var queueNaam = configuratie["RabbitMQ:QueueName"] ?? "BestelAppQueue";

await channel.QueueDeclareAsync(
    //de naam van de queue
    queue: queueNaam,
    //messages blijven bewaard ook als de broker opnieuw opstart, als het False is en de app crasht zijn de messages weg
    durable: true,
    //als de subscriber weg is, wordt de queue verwijderd
    exclusive: false,
    autoDelete: false,
    arguments: null);

Console.WriteLine("✅ Consumer is klaar!");
Console.WriteLine($"👂 Wachten op bestellingen in queue '{queueNaam}'...");
Console.WriteLine("=====================================\n");

// ========================================
// STAP 4: Consumer opzetten
// ========================================
var consumer = new AsyncEventingBasicConsumer(channel);

//messages krijgen van de queue
consumer.ReceivedAsync += async (sender, eventArgs) =>
{
    //byte array van de message in de queue om daarna naar UTF8 te omzetten 
    byte[] body = eventArgs.Body.ToArray();
    //omzetten van byte array naar string
    string berichtTekst = Encoding.UTF8.GetString(body);

    Console.WriteLine($"\n📬 Nieuw bericht ontvangen!");
    Console.WriteLine($"📄 Bericht: {berichtTekst}");
    Console.WriteLine("-------------------------------------");

    try
    {
        // Probeer JSON te parsen
        OrderMessage? bestelling = null;
        bool isJson = false;

        try
        {
            bestelling = JsonSerializer.Deserialize<OrderMessage>(berichtTekst);
            if (bestelling != null)
            {
                isJson = true;
                Console.WriteLine($"✅ JSON parsing succesvol!");
                Console.WriteLine($"📦 Order ID: {bestelling.OrderId}");
                Console.WriteLine($"👟 Product: {bestelling.Brand} {bestelling.Name}");
                Console.WriteLine($"📏 Maat: {bestelling.Size}");
                Console.WriteLine($"💰 Prijs: €{bestelling.Price}");
            }
        }
        catch (JsonException)
        {
            Console.WriteLine("⚠️ Bericht is geen JSON, gebruik fallback...");
        }

        // Verstuur naar Salesforce
        SalesforceResultaat resultaat;

        if (isJson && bestelling != null)
        {
            // Verstuur als OrderMessage
            resultaat = await salesforceClient.StuurBestellingAsync(bestelling);
        }
        else
        {
            // Verstuur als fallback (alleen Description veld)
            resultaat = await salesforceClient.StuurFallbackBerichtAsync(berichtTekst);
        }

        // Beslis ACK of NACK op basis van het resultaat
        if (resultaat.IsSuccesvol)
        {
            // Success! → ACK
            Console.WriteLine("✅ Salesforce operatie succesvol → ACK");
            await ((AsyncEventingBasicConsumer)sender).Channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
        }
        else if (resultaat.IsHerhaalbaar)
        {
            // Tijdelijke fout (429, 5xx) → NACK met requeue=true
            Console.WriteLine("⏳ Tijdelijke fout → NACK (requeue=TRUE)");
            Console.WriteLine("   Bericht gaat terug in de queue voor later...");
            await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(
                eventArgs.DeliveryTag, 
                multiple: false, 
                requeue: true);
        }
        else
        {
            // Permanente fout (400, 4xx) → NACK met requeue=false
            Console.WriteLine("❌ Permanente fout → NACK (requeue=FALSE)");
            Console.WriteLine("   Bericht gaat naar Dead Letter Queue...");
            await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(
                eventArgs.DeliveryTag, 
                multiple: false, 
                requeue: false);
        }
    }
    catch (Exception ex)
    {
        // Bij onverwachte fouten → NACK met requeue=false
        Console.WriteLine($"❌ Onverwachte fout: {ex.Message}");
        Console.WriteLine($"📍 Stack trace: {ex.StackTrace}");
        Console.WriteLine("❌ NACK (requeue=FALSE)");
        
        await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(
            eventArgs.DeliveryTag, 
            multiple: false, 
            requeue: false);
    }

    Console.WriteLine("=====================================");
    Console.WriteLine($"👂 Wachten op volgende bericht...\n");
};

await channel.BasicConsumeAsync(
    queueNaam,
    //manueel de berichten bevestigen
    autoAck: false,
    consumer);

Console.WriteLine("Druk op ENTER om te stoppen...");
Console.ReadLine();
