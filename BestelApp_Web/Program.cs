using BestelApp_Models;
using BestelApp_Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

//DbContext toevoegen voor Entity Framework (configuratie staat in AppDbContext.cs)
builder.Services.AddDbContext<AppDbContext>();

// RabbitMQ Service toevoegen zodat we kunnen bestellen
builder.Services.AddSingleton<RabbitMQService>();

//Identity configureren voor login/register functionaliteit
builder.Services.AddIdentity<Users, IdentityRole>(opties =>
{
    // Wachtwoord eisen (voor development makkelijk)
    opties.Password.RequireDigit = true;
    opties.Password.RequiredLength = 6;
    opties.Password.RequireNonAlphanumeric = false;
    opties.Password.RequireUppercase = true;
    opties.Password.RequireLowercase = true;

    // Gebruiker eisen
    opties.User.RequireUniqueEmail = true;

    // Sign in opties
    opties.SignIn.RequireConfirmedAccount = false; // Geen e-mailbevestiging nodig
    opties.SignIn.RequireConfirmedEmail = false; // Voor development
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

//Cookie instellingen voor login
builder.Services.ConfigureApplicationCookie(opties =>
{
    opties.LoginPath = "/Account/Login"; // Waar moet gebruiker naartoe als niet ingelogd
    opties.LogoutPath = "/Account/Logout";
    opties.AccessDeniedPath = "/Account/AccessDenied";
    opties.ExpireTimeSpan = TimeSpan.FromDays(7); // Cookie blijft 7 dagen geldig
    opties.SlidingExpiration = true; // Vernieuw cookie bij elke request
});

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


//BELANGRIJK: Authentication moet VOOR Authorization
app.UseAuthentication(); // Checkt wie je bent
app.UseAuthorization();  // Checkt wat je mag doen


app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Map Razor Pages (nodig voor Identity)
app.MapRazorPages();

app.Run();
