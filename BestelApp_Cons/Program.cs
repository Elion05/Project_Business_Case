using System.Text;
using System.Text.Json;
using BestelApp_Cons.Models;
using BestelApp_Cons.Salesforce;
using BestelApp_Cons.Services;
using BestelApp_Shared;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;


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
    .AddEnvironmentVariables() // Hiermee kunnen we secrets uit GitHub/Environment lezen
    .Build();

// ========================================
// STAP 2: Services initialiseren
// ========================================
Console.WriteLine("🔐 Salesforce services initialiseren...");

var salesforceAuthService = new SalesforceAuthService(configuratie);
var salesforceClient = new SalesforceClient(configuratie, salesforceAuthService);

Console.WriteLine("🗄️  ProcessedOrdersTracker initialiseren...");
var processedOrdersTracker = new ProcessedOrdersTracker(cacheDuration: TimeSpan.FromHours(24));

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
var deadLetterQueueNaam = $"{queueNaam}_DLQ";

// ========================================
// STAP 4: Dead Letter Queue configureren
// ========================================
Console.WriteLine($"💀 Dead Letter Queue configureren: {deadLetterQueueNaam}");

// Maak Dead Letter Queue aan
await channel.QueueDeclareAsync(
    queue: deadLetterQueueNaam,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null);

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
    var messageId = eventArgs.BasicProperties?.MessageId ?? "Unknown";
    var correlationId = eventArgs.BasicProperties?.CorrelationId ?? "Unknown";

    Console.WriteLine("\n═══════════════════════════════════════");
    Console.WriteLine($"📬 Nieuw bericht ontvangen!");
    Console.WriteLine($"   MessageId: {messageId}");
    Console.WriteLine($"   CorrelationId: {correlationId}");
    Console.WriteLine($"   DeliveryTag: {eventArgs.DeliveryTag}");
    Console.WriteLine($"   Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine("───────────────────────────────────────");

    totalProcessed++;

    try
    {
        // STAP 1: Deserialiseer JSON message
        byte[] body = eventArgs.Body.ToArray();
        string encryptedTekst = Encoding.UTF8.GetString(body);

        Console.WriteLine("🔐 Encrypted Message:");
        Console.WriteLine(encryptedTekst);

        string berichtTekst;
        try
        {
            berichtTekst = EncryptionHelper.Decrypt(encryptedTekst);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Decryptie gefaald: {ex.Message}");
            // Fallback: misschien was het onversleuteld?
            berichtTekst = encryptedTekst;
        }

        Console.WriteLine("📄 Decrypted JSON Message:");
        Console.WriteLine(berichtTekst);
        Console.WriteLine("───────────────────────────────────────");

        OrderMessage? order = null;
        try
        {
            order = JsonSerializer.Deserialize<OrderMessage>(berichtTekst);
            if (order != null)
            {
                Console.WriteLine("✓ JSON deserialisatie succesvol");
                Console.WriteLine($"  Order ID: {order.OrderId}");
                Console.WriteLine($"  User: {order.UserName} ({order.UserEmail})");
                Console.WriteLine($"  Items: {order.Items?.Count ?? 0}");
                Console.WriteLine($"  Totaal: €{order.TotalPrice}");
            }
        }
        catch (JsonException jsonEx)
        {
            Console.WriteLine($"✗ JSON deserialisatie gefaald: {jsonEx.Message}");
            Console.WriteLine("❌ PERMANENTE FOUT → NACK (requeue=FALSE) → DLQ");

            await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);

            totalFailed++;
            return;
        }

        if (order == null)
        {
            Console.WriteLine("✗ Order is null na deserialisatie");
            Console.WriteLine("❌ PERMANENTE FOUT → NACK (requeue=FALSE) → DLQ");

            await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);

            totalFailed++;
            return;
        }

        // STAP 2: Valideer verplichte velden
        Console.WriteLine("───────────────────────────────────────");
        Console.WriteLine("🔍 Validatie van verplichte velden...");

        var validationResult = OrderValidator.ValidateOrder(order);

        if (!validationResult.IsValid)
        {
            Console.WriteLine($"✗ Validatie gefaald:");
            foreach (var error in validationResult.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
            Console.WriteLine("❌ PERMANENTE FOUT → NACK (requeue=FALSE) → DLQ");

            await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);

            totalFailed++;
            return;
        }

        Console.WriteLine("✓ Validatie succesvol - alle verplichte velden aanwezig");

        // STAP 3: Check of order al verwerkt is
        Console.WriteLine("───────────────────────────────────────");
        Console.WriteLine("🔍 Check op dubbele verwerking...");

        if (processedOrdersTracker.IsOrderAlreadyProcessed(order.OrderId))
        {
            Console.WriteLine($"⚠️  DUPLICAAT GEDETECTEERD: Order {order.OrderId} al verwerkt!");
            Console.WriteLine("✓ ACK (bericht wordt verwijderd, niet opnieuw verwerkt)");

            await ((AsyncEventingBasicConsumer)sender).Channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false);

            totalDuplicates++;

            // Toon statistieken
            var stats = processedOrdersTracker.GetStats();
            Console.WriteLine($"📊 Statistieken: {stats}");
            return;
        }

        Console.WriteLine("✓ Order nog niet verwerkt, doorgaan...");

        // STAP 4: Zet order om naar Salesforce formaat en verstuur
        Console.WriteLine("───────────────────────────────────────");
        Console.WriteLine("🔄 Order omzetten naar Salesforce formaat...");
        Console.WriteLine($"   Klant: {order.UserName}");
        Console.WriteLine($"   Items: {order.Items.Count}");
        Console.WriteLine($"   Adres: {order.ShippingAddress?.FullAddress}");

        Console.WriteLine("\n📤 Versturen naar Salesforce...");
        var resultaat = await salesforceClient.StuurBestellingAsync(order);

        // STAP 5: Verwerk resultaat en besluit ACK/NACK/Retry
        Console.WriteLine("───────────────────────────────────────");

        if (resultaat.IsSuccesvol)
        {
            // SUCCES! → ACK
            Console.WriteLine("✅ Salesforce operatie SUCCESVOL");
            Console.WriteLine($"   Status Code: {resultaat.StatusCode}");
            Console.WriteLine("✓ ACK (bericht wordt verwijderd uit queue)");

            await ((AsyncEventingBasicConsumer)sender).Channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false);

            // Markeer als verwerkt
            processedOrdersTracker.MarkAsProcessed(order.OrderId);

            totalSucceeded++;
        }
        else if (resultaat.IsHerhaalbaar)
        {
            // TIJDELIJKE FOUT → RETRY (NACK met requeue=true)
            Console.WriteLine("⚠️  Tijdelijke fout bij Salesforce");
            Console.WriteLine($"   Status Code: {resultaat.StatusCode}");
            Console.WriteLine($"   Foutmelding: {resultaat.Foutmelding}");
            Console.WriteLine("🔄 NACK (requeue=TRUE) - bericht gaat terug in queue voor retry");

            await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: true);

            // Kleine delay om thundering herd te voorkomen
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        else
        {
            // PERMANENTE FOUT → DLQ (NACK met requeue=false)
            Console.WriteLine("❌ Permanente fout bij Salesforce");
            Console.WriteLine($"   Status Code: {resultaat.StatusCode}");
            Console.WriteLine($"   Foutmelding: {resultaat.Foutmelding}");
            Console.WriteLine($"💀 NACK (requeue=FALSE) - bericht gaat naar Dead Letter Queue");

            await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);

            totalFailed++;
        }
    }
    catch (Exception ex)
    {
        // ONVERWACHTE FOUT → DLQ
        Console.WriteLine("───────────────────────────────────────");
        Console.WriteLine($"❌ ONVERWACHTE FOUT:");
        Console.WriteLine($"   Type: {ex.GetType().Name}");
        Console.WriteLine($"   Message: {ex.Message}");
        Console.WriteLine($"   Stack: {ex.StackTrace}");
        Console.WriteLine("💀 NACK (requeue=FALSE) - bericht gaat naar Dead Letter Queue");

        await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(
            eventArgs.DeliveryTag,
            multiple: false,
            requeue: false);

        totalFailed++;
    }

    // Toon statistieken
    Console.WriteLine("───────────────────────────────────────");
    Console.WriteLine("📊 Totale Statistieken:");
    Console.WriteLine($"   Verwerkt: {totalProcessed}");
    Console.WriteLine($"   Succesvol: {totalSucceeded}");
    Console.WriteLine($"   Gefaald: {totalFailed}");
    Console.WriteLine($"   Duplicaten: {totalDuplicates}");
    var trackerStats = processedOrdersTracker.GetStats();
    Console.WriteLine($"   Cache: {trackerStats}");
    Console.WriteLine("═══════════════════════════════════════\n");
    Console.WriteLine($" Wachten op volgende bericht...\n");
};

// Start consuming met manual ACK
await channel.BasicConsumeAsync(
    queueNaam,
    autoAck: false,  // Handmatig ACK/NACK
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

