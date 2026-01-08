using BestelApp_Models;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
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
    queue: "BestelAppQueue",
    //messages blijven bewaard ook als de broker opnieuw opstart, als het False is en de app crasht zijn de messages weg
    durable: true,
    //als de subscriber weg is, wordt de queue verwijderd
    exclusive: false,
    autoDelete: false,
    arguments: null
    );

for (int i = 0; i < 10; i++)
{
    var message = $"{DateTime.UtcNow} - {Guid.CreateVersion7()}";
    var body = Encoding.UTF8.GetBytes(message);

    await channel.BasicPublishAsync(
        exchange: string.Empty,
        routingKey: "message",
        mandatory: true,

        //telling the broker to persiste the message to queue
        basicProperties: new BasicProperties { Persistent = true },

        body: body
        );
    Console.WriteLine($"Sent {message}");

    await Task.Delay(2000);
}

var builder = WebApplication.CreateBuilder(args);


//dbcontext toevoegen voor Entity Framework
builder.Services.AddDbContext<AppDbContext>();



//Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate();
        AppDbContext.Seeder(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

//Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    //The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
