using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BestelApp_Models
{
    /// <summary>
    /// ApplicationDbContext voor database toegang
    /// Gebruikt dependency injection voor configuratie
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<Users>
    {
        // Constructor met DbContextOptions (dependency injection - runtime)
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Parameterloze constructor voor EF Design-time tools (migrations)
        public ApplicationDbContext()
            : base()
        {
        }

        // DbSets voor database tabellen
        public DbSet<Users> AppUsers { get; set; }
        public DbSet<Shoe> Shoes { get; set; }
        public DbSet<ShoeVariant> ShoeVariants { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Favorite> Favorites { get; set; }

        // OnConfiguring voor design-time (migrations)
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Alleen gebruiken als nog niet geconfigureerd (voor migrations)
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("Data Source=BestelApp.db");
            }
        }

        // OnModelCreating voor entiteit configuratie
        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Roep base.OnModelCreating aan voor Identity configuratie
            base.OnModelCreating(builder);

            // Configureer Category → Shoe relatie (1-to-many)
            builder.Entity<Shoe>()
                .HasOne(s => s.Category)
                .WithMany(c => c.Shoes)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configureer Shoe → ShoeVariant relatie (1-to-many)
            builder.Entity<ShoeVariant>()
                .HasOne(sv => sv.Shoe)
                .WithMany(s => s.Variants)
                .HasForeignKey(sv => sv.ShoeId)
                .OnDelete(DeleteBehavior.Cascade); // Als schoen verwijderd, verwijder varianten ook

            // Configureer Cart → User relatie (1-to-many)
            builder.Entity<Cart>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade); // Als user verwijderd, verwijder cart ook

            // Configureer CartItem → Cart relatie (many-to-1)
            builder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.Items)
                .HasForeignKey(ci => ci.CartId)
                .OnDelete(DeleteBehavior.Cascade); // Als cart verwijderd, verwijder items ook

            // Configureer CartItem → ShoeVariant relatie
            builder.Entity<CartItem>()
                .HasOne(ci => ci.ShoeVariant)
                .WithMany()
                .HasForeignKey(ci => ci.ShoeVariantId)
                .OnDelete(DeleteBehavior.Restrict); // Bescherm ShoeVariant tegen verwijdering

            // Configureer Order → User relatie (many-to-1)
            builder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Bescherm User tegen verwijdering

            // Configureer OrderItem → Order relatie (many-to-1)
            // Gebruik Order.OrderId (string) als principal key in plaats van Order.Id (long)
            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .HasPrincipalKey(o => o.OrderId) // Link naar OrderId (string) ipv Id (long)
                .OnDelete(DeleteBehavior.Cascade); // Als order verwijderd, verwijder items ook

            // Configureer OrderItem → ShoeVariant relatie
            builder.Entity<OrderItem>()
                .HasOne(oi => oi.ShoeVariant)
                .WithMany()
                .HasForeignKey(oi => oi.ShoeVariantId)
                .OnDelete(DeleteBehavior.Restrict); // Bescherm ShoeVariant tegen verwijdering

            // Index voor snelle lookup van cart per user
            builder.Entity<Cart>()
                .HasIndex(c => c.UserId);

            // Index voor snelle lookup van orders per user
            builder.Entity<Order>()
                .HasIndex(o => o.UserId);

            // Index voor snelle lookup van OrderId
            builder.Entity<Order>()
                .HasIndex(o => o.OrderId)
                .IsUnique();

            // Configureer Favorite → User relatie
            builder.Entity<Favorite>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade); // Als user verwijderd, verwijder favorieten ook

            // Configureer Favorite → Shoe relatie
            builder.Entity<Favorite>()
                .HasOne(f => f.Shoe)
                .WithMany()
                .HasForeignKey(f => f.ShoeId)
                .OnDelete(DeleteBehavior.Restrict); // Bescherm Shoe tegen verwijdering

            // Unieke constraint: 1 user kan 1 product maar 1x favorieten
            builder.Entity<Favorite>()
                .HasIndex(f => new { f.UserId, f.ShoeId })
                .IsUnique();
        }
    }
}
