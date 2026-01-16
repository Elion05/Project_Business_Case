using BestelApp_API.Services;
using BestelApp_Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ApplicationDbContext toevoegen
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity configureren (voor Authorization)
builder.Services.AddIdentity<Users, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Authentication/Authorization
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// Add services to the container
builder.Services.AddControllers();

// RabbitMQ Service registreren
builder.Services.AddSingleton<RabbitMQService>();

// Swagger/OpenAPI configuratie
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS configuratie (zodat WebApp kan communiceren met API)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp", policy =>
    {
        policy.WithOrigins("https://localhost:5001", "http://localhost:5000") // WebApp URL's
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// CORS moet voor Authentication/Authorization
app.UseCors("AllowWebApp");

// Authentication MOET voor Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Database seeden bij opstarten
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<Users>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        
        // EERST: Pas migrations toe
        Console.WriteLine("📦 Toepassen database migrations...");
        await context.Database.MigrateAsync();
        Console.WriteLine("✅ Migrations toegepast");
        
        // DAN: Seed data
        Console.WriteLine("🌱 Starten database seeding...");
        var seeder = new DbSeeder(context, userManager, roleManager);
        await seeder.SeedAsync();
        
        Console.WriteLine("✅ Database migrations en seeding voltooid");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ Fout bij database seeding");
    }
}

Console.WriteLine("🚀 Backend API is gestart");
Console.WriteLine("📍 Swagger UI: https://localhost:7001/swagger");
Console.WriteLine("📍 API endpoint: https://localhost:7001/api/orders");

app.Run();
