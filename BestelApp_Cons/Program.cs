using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using BestelApp_Cons.Models;
using BestelApp_Cons.Services;
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
// STAP 2: Services initialiseren
// ========================================
Console.WriteLine("🔐 Services initialiseren...");

var salesforceAuthService = new SalesforceAuthService(configuratie);
var salesforceClient = new SalesforceClient(configuratie, salesforceAuthService);

// Idempotency DB pad (fixed location for backup consistency)
var dbPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../data/idempotency.db"));
var idempotencyService = new IdempotencyService(dbPath);

// Initialize DB
Console.WriteLine($"💾 Idempotency DB initialiseren op: {dbPath}");
await idempotencyService.InitializeAsync();

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

// DLQ Argumenten
var queueArgs = new Dictionary<string, object?>
{
    { "x-dead-letter-exchange", "" }, // Default exchange
    { "x-dead-letter-routing-key", queueNaam + "_DLQ" } // DLQ naam conventie
};

await channel.QueueDeclareAsync(
    queue: queueNaam,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: queueArgs); // Toegevoegde DLQ argumenten

Console.WriteLine("✅ Consumer is klaar!");
Console.WriteLine($"👂 Wachten op bestellingen in queue '{queueNaam}'...");
Console.WriteLine("=====================================\n");

// ========================================
// STAP 4: Consumer opzetten
// ========================================
var consumer = new AsyncEventingBasicConsumer(channel);

consumer.ReceivedAsync += async (sender, eventArgs) =>
{
    byte[] body = eventArgs.Body.ToArray();
    string berichtTekst = Encoding.UTF8.GetString(body);
    string messageId = eventArgs.BasicProperties.MessageId ?? Guid.NewGuid().ToString(); // Fallback ID

    Console.WriteLine($"\n📬 Nieuw bericht ontvangen! ID: {messageId}");

    try
    {
        // 1. Idempotency Check
        var state = await idempotencyService.GetStateAsync(messageId);
        if (state?.Status == "Processed")
        {
            Console.WriteLine($"⏭️ Bericht {messageId} is al verwerkt. Skipping (ACK).");
            await ((AsyncEventingBasicConsumer)sender).Channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
            return;
        }

        // 2. Markeer als Processing
        await idempotencyService.MarkProcessingAsync(messageId, berichtTekst);

        Console.WriteLine($"📄 Bericht: {berichtTekst}");
        Console.WriteLine("-------------------------------------");

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
                Console.WriteLine($"💰 Prijs: €{bestelling.Price}");
            }
        }
        catch (JsonException)
        {
            Console.WriteLine("⚠️ Bericht is geen JSON, gebruik fallback...");
        }

        // 3. Verstuur naar Salesforce
        SalesforceResultaat resultaat;

        if (isJson && bestelling != null)
        {
            resultaat = await salesforceClient.StuurBestellingAsync(bestelling);
        }
        else
        {
            resultaat = await salesforceClient.StuurFallbackBerichtAsync(berichtTekst);
        }

        // 4. Verwerk resultaat
        if (resultaat.IsSuccesvol)
        {
            // Success! -> Update DB -> ACK
            Console.WriteLine("✅ Salesforce operatie succesvol → ACK");
            await idempotencyService.MarkProcessedAsync(messageId);
            await ((AsyncEventingBasicConsumer)sender).Channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
        }
        else if (resultaat.IsHerhaalbaar)
        {
            // Tijdelijke fout -> Update DB (Failed? Of blijven staan op Processing?)
            // Requirement zegt: "bij error Failed... + NACK(requeue:false)" -> Maar tijdelijke fout wil je vaak retryen.
            // Echter, user prompt zegt specifiek: "bij error Failed + RetryCount++ ... + NACK(requeue:false)"
            // Als we strikt de prompt volgen voor ALLE errors:
            
            Console.WriteLine($"⏳ Tijdelijke fout ({resultaat.Foutmelding}) → Mark Failed & NACK (requeue=FALSE)"); 
            // Noot: Normaal zou je hier requeue=true doen OF naar DLQ sturen. User prompt zegt expliciet NACK(requeue:false) -> DLQ.
            // Dus we behandelen retry via DLQ loop of latere process.
            
            await idempotencyService.MarkFailedAsync(messageId, resultaat.Foutmelding ?? "Unknown Temporary Error");
            await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
        }
        else
        {
            // Permanente fout
            Console.WriteLine($"❌ Permanente fout ({resultaat.Foutmelding}) → Mark Failed & NACK (requeue=FALSE)");
            await idempotencyService.MarkFailedAsync(messageId, resultaat.Foutmelding ?? "Unknown Permanent Error");
            await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Onverwachte fout: {ex.Message}");
        await idempotencyService.MarkFailedAsync(messageId, ex.Message);
        await ((AsyncEventingBasicConsumer)sender).Channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
    }

    Console.WriteLine("=====================================");
    Console.WriteLine($"👂 Wachten op volgende bericht...\n");
};

await channel.BasicConsumeAsync(
    queueNaam,
    autoAck: false,
    consumer);

Console.WriteLine("Druk op ENTER om te stoppen...");
Console.ReadLine();
