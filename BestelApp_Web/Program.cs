using BestelApp_Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using BestelApp_Web.Services;

var builder = WebApplication.CreateBuilder(args);


//dbcontext toevoegen voor Entity Framework
builder.Services.AddDbContext<AppDbContext>();

//RabbitMQ Service toevoegen zodat we kunnen bestellen
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
        await AppDbContext.Seeder(context);
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
    app.UseHttpsRedirection();
}


app.UseRouting();

//BELANGRIJK: Authentication moet VOOR Authorization
app.UseAuthentication(); // Checkt wie je bent
app.UseAuthorization();  // Checkt wat je mag doen

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
