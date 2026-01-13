using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace BestelApp_Models
{
    // AppDbContext erft over van IdentityDbContext om Identity te ondersteunen
    public class AppDbContext : IdentityDbContext<Users>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Users> AppUsers { get; set; }
        public DbSet<Shoe> Shoes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // De connectiestring is hier hardcoded ingesteld volgens de GitHub repo
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("Data Source=BestelApp.db");
            }
        }


        // Seeder methode om de database te vullen met beginwaarden
        public static async Task Seeder(AppDbContext context)
        {
            await BestelApp_Models.Users.Seeder(context);

            if (!context.Shoes.Any())
            {
                context.Shoes.AddRange(Shoe.SeedingData());
                await context.SaveChangesAsync();
            }
        }
    }
}
