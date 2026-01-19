using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using BestelApp_Cons.Models;
using BestelApp_Cons.Services;
using BestelApp_Cons.Salesforce;
using BestelApp_Cons.Services;

Console.WriteLine("═══════════════════════════════════════");
Console.WriteLine("  🐰 BestelApp RabbitMQ Consumer");
Console.WriteLine("  📦 Order Processing Service");
Console.WriteLine("═══════════════════════════════════════\n");

// ========================================
// STAP 1: Configuratie laden
// ========================================
Console.WriteLine("📋 Configuratie laden...");

var configuratie = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// ========================================
// STAP 2: Services initialiseren
// ========================================
Console.WriteLine("🔐 Services initialiseren...");

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
    Password = configuratie["RabbitMQ:Password"] ?? "user123!",
    RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
    SocketReadTimeout = TimeSpan.FromSeconds(30),
    SocketWriteTimeout = TimeSpan.FromSeconds(30)
};

using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

var queueNaam = configuratie["RabbitMQ:QueueName"] ?? "BestelAppQueue";

await channel.QueueDeclareAsync(
    //de naam van de queue
    queue: queueNaam,
    //messages blijven bewaard ook als de broker opnieuw opstart, als het False is en de app crasht zijn de messages weg
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: queueArgs); // Toegevoegde DLQ argumenten

// ========================================
// STAP 5: Hoofd Queue configureren met DLQ
// ========================================
Console.WriteLine($"📬 Hoofd Queue configureren: {queueNaam}");

// Configureer queue met Dead Letter Exchange
var queueArgs = new Dictionary<string, object>
{
    // Messages die gereject worden (NACK met requeue=false) gaan naar DLQ
    { "x-dead-letter-exchange", "" },  // Default exchange
    { "x-dead-letter-routing-key", deadLetterQueueNaam }
};

await channel.QueueDeclareAsync(
    queue: queueNaam,
    durable: true,        // Queue blijft bestaan na restart
    exclusive: false,      // Niet exclusief voor 1 connectie
    autoDelete: false,     // Blijft bestaan zonder consumers
    arguments: queueArgs); // Met DLQ configuratie

Console.WriteLine("\n✅ Consumer is klaar!");
Console.WriteLine($"👂 Luisteren naar: {queueNaam}");
Console.WriteLine($"💀 Dead Letter Queue: {deadLetterQueueNaam}");
Console.WriteLine($"🔄 Cache duur: 24 uur");
Console.WriteLine("═══════════════════════════════════════\n");

// Statistieken
var totalProcessed = 0;
var totalSucceeded = 0;
var totalFailed = 0;
var totalDuplicates = 0;

// ========================================
// STAP 6: Consumer opzetten
// ========================================
var consumer = new AsyncEventingBasicConsumer(channel);

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
            order = JsonSerializer.Deserialize<OrderMessage>(berichtTekst);
            if (order != null)
            {
                isJson = true;
                Console.WriteLine($"✅ JSON parsing succesvol!");
                Console.WriteLine($"📦 Order ID: {bestelling.OrderId}");
                Console.WriteLine($"👟 Product: {bestelling.Brand} {bestelling.Name}");
                Console.WriteLine($"📏 Maat: {bestelling.Size}");
                Console.WriteLine($"💰 Prijs: €{bestelling.Price}");
            }
        }
        catch (JsonException jsonEx)
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

// Start consuming met manual ACK
await channel.BasicConsumeAsync(
    queueNaam,
    //manueel de berichten bevestigen
    autoAck: false,
    consumer);

Console.WriteLine("✓ Consumer actief - druk op CTRL+C om te stoppen...\n");

// Wacht oneindig (of tot CTRL+C)
var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};

try
{
    await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
}
catch (TaskCanceledException)
{
    // Normal shutdown
}

Console.WriteLine("\n🛑 Consumer wordt gestopt...");
Console.WriteLine($"📊 Finale statistieken:");
Console.WriteLine($"   Totaal verwerkt: {totalProcessed}");
Console.WriteLine($"   Succesvol: {totalSucceeded}");
Console.WriteLine($"   Gefaald: {totalFailed}");
Console.WriteLine($"   Duplicaten: {totalDuplicates}");
Console.WriteLine("\n👋 Tot ziens!");

