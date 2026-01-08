using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace BestelApp_Models
{
    public class AppDbContext : IdentityDbContext<Users>
    {
        public DbSet<Users> AppUsers { get; set; }
        public DbSet<Shoe> Shoes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=BestelAppDb;Trusted_Connection=true;MultipleActiveResultSets=true");
            }
        }


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