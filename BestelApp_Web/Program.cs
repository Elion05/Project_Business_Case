using BestelApp_Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using BestelApp_Web.Services;

var builder = WebApplication.CreateBuilder(args);

//ApplicationDbContext toevoegen voor Entity Framework met connection string
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

//Identity configureren met de aangepaste 'Users' klasse en rollen
builder.Services.AddIdentity<Users, IdentityRole>(options =>
{
    // Wachtwoord eisen
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Gebruiker eisen
    options.User.RequireUniqueEmail = true;

    // Sign in opties
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultUI()
.AddDefaultTokenProviders();

//Cookie instellingen voor login
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Backend API Service toevoegen (vervangt directe RabbitMQ connectie)
builder.Services.AddHttpClient<OrderApiService>();

// Services toevoegen voor Controllers en Views
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // Nodig voor Identity pagina's

// Localization configuratie voor decimale getallen (accepteert punt en komma)
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new System.Globalization.CultureInfo("nl-BE") };
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("nl-BE");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

var app = builder.Build();

// WebApp gebruikt DEZELFDE database als de Backend API!
// Geen migrations of seeding nodig - de API doet dat al!
Console.WriteLine("📊 WebApp gebruikt gedeelde database: ../BestelApp_API/BestelApp.db");
Console.WriteLine("✅ Database wordt beheerd door Backend API");

// Configureer de HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Gebruik localization voor decimale getallen
app.UseRequestLocalization();

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
