using BestelApp_Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using BestelApp_Web.Services;

var builder = WebApplication.CreateBuilder(args);

//DbContext toevoegen voor Entity Framework (configuratie staat in AppDbContext.cs)
builder.Services.AddDbContext<AppDbContext>();

//Identity configureren met de aangepaste 'Users' klasse
//Dit is nodig om de inlogfunctionaliteit werkend te krijgen
builder.Services.AddIdentity<Users, IdentityRole>(options => 
    {
        options.SignIn.RequireConfirmedAccount = false; // Geen e-mailbevestiging nodig voor nu
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultUI() // Voegt de standaard Identity UI-pagina's toe
    .AddDefaultTokenProviders();

// RabbitMQ Service toevoegen zodat we kunnen bestellen
builder.Services.AddSingleton<RabbitMQService>();

// Services toevoegen voor Controllers en Views
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // Nodig voor Identity pagina's

var app = builder.Build();

// Database migraties uitvoeren en data seeden bij het opstarten
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate(); // Voert database wijzigingen door
        await AppDbContext.Seeder(context); // Voegt testdata toe (Users en Shoes)
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Er is een fout opgetreden bij het seeden van de database.");
    }
}

// Configureer de HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Authentication MOET voor Authorization staan om inloggen te laten werken
app.UseAuthentication(); 
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Map Razor Pages (nodig voor Identity)
app.MapRazorPages();

app.Run();
