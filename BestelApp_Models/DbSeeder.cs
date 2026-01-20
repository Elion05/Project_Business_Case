using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BestelApp_Models
{
    /// <summary>
    /// Centrale seeder class voor database initialisatie
    /// Gebruikt UserManager en RoleManager via dependency injection
    /// </summary>
    public class DbSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public DbSeeder(
            ApplicationDbContext context,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        /// <summary>
        /// Seed alle data: rollen, users, categories, shoes en variants
        /// </summary>
        public async Task SeedAsync()
        {
            // GEEN MigrateAsync() hier - dit gebeurt al in Program.cs!
            // await _context.Database.MigrateAsync();

            // Seed in volgorde: rollen eerst, dan users, dan categories, dan shoes
            await SeedRolesAsync();
            await SeedUsersAsync();
            await SeedCategoriesAsync();
            await SeedShoesAsync();
        }

        /// <summary>
        /// Seed rollen: Admin, User, System_Admin
        /// </summary>
        public async Task SeedRolesAsync()
        {
            string[] roles = { "Admin", "User", "System_Admin" };

            foreach (var roleName in roles)
            {
                // Check of rol al bestaat
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    var role = new IdentityRole
                    {
                        Name = roleName,
                        NormalizedName = roleName.ToUpper()
                    };

                    var result = await _roleManager.CreateAsync(role);

                    if (result.Succeeded)
                    {
                        Console.WriteLine($"✅ Rol '{roleName}' aangemaakt");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Fout bij aanmaken rol '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                }
            }
        }

        /// <summary>
        /// Seed users: Admin, User en System_Admin
        /// </summary>
        public async Task SeedUsersAsync()
        {
            // Admin user
            await CreateUserIfNotExistsAsync(
                userName: "admin",
                email: "admin@bestelapp.be",
                password: "Admin123!",
                firstName: "Admin",
                lastName: "Beheerder",
                address: "Adminstraat 1",
                city: "Brussel",
                postalCode: "1000",
                country: "België",
                role: "Admin"
            );

            // Normale user
            await CreateUserIfNotExistsAsync(
                userName: "user",
                email: "user@bestelapp.be",
                password: "User123!",
                firstName: "Jan",
                lastName: "Janssens",
                address: "Kerkstraat 10",
                city: "Antwerpen",
                postalCode: "2000",
                country: "België",
                role: "User"
            );

            // System Admin
            await CreateUserIfNotExistsAsync(
                userName: "system_admin",
                email: "sysadmin@bestelapp.be",
                password: "SysAdmin123!",
                firstName: "System",
                lastName: "Administrator",
                address: "Tech Lane 99",
                city: "Gent",
                postalCode: "9000",
                country: "België",
                role: "System_Admin"
            );
        }

        /// <summary>
        /// Helper methode om user aan te maken
        /// </summary>
        private async Task CreateUserIfNotExistsAsync(
            string userName,
            string email,
            string password,
            string firstName,
            string lastName,
            string address,
            string city,
            string postalCode,
            string country,
            string role)
        {
            // Check of user al bestaat
            var existingUser = await _userManager.FindByNameAsync(userName);
            if (existingUser != null)
            {
                return; // User bestaat al
            }

            // Maak nieuwe user
            var user = new Users
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                Address = address,
                City = city,
                PostalCode = postalCode,
                Country = country
            };

            // Maak user met wachtwoord
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                Console.WriteLine($"✅ User '{userName}' aangemaakt");

                // Voeg user toe aan rol
                var roleResult = await _userManager.AddToRoleAsync(user, role);

                if (roleResult.Succeeded)
                {
                    Console.WriteLine($"   → Rol '{role}' toegekend aan '{userName}'");
                }
                else
                {
                    Console.WriteLine($"   ❌ Fout bij toekennen rol: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                Console.WriteLine($"❌ Fout bij aanmaken user '{userName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        /// <summary>
        /// Seed categories
        /// </summary>
        private async Task SeedCategoriesAsync()
        {
            // Check of er al categorieën zijn
            if (await _context.Categories.AnyAsync())
            {
                return; // Al data aanwezig
            }

            var categories = new List<Category>
            {
                new Category { Name = "Sport", Description = "Sportschoenen voor actieve sporten", IsActive = true },
                new Category { Name = "Casual", Description = "Comfortabele dagelijkse schoenen", IsActive = true },
                new Category { Name = "Formal", Description = "Nette schoenen voor formele gelegenheden", IsActive = true },
                new Category { Name = "Sneakers", Description = "Trendy sneakers", IsActive = true },
                new Category { Name = "Running", Description = "Hardloopschoenen", IsActive = true }
            };

            await _context.Categories.AddRangeAsync(categories);
            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ {categories.Count} categorieën toegevoegd aan database");
        }

        /// <summary>
        /// Seed shoes met nieuwe structuur (Category, Gender, Variants)
        /// </summary>
        private async Task SeedShoesAsync()
        {
            // Check of er al schoenen zijn
            if (await _context.Shoes.AnyAsync())
            {
                return; // Al data aanwezig
            }

            // Haal categorieën op
            var sportCat = await _context.Categories.FirstAsync(c => c.Name == "Sport");
            var casualCat = await _context.Categories.FirstAsync(c => c.Name == "Casual");
            var sneakersCat = await _context.Categories.FirstAsync(c => c.Name == "Sneakers");
            var runningCat = await _context.Categories.FirstAsync(c => c.Name == "Running");

            var shoes = new List<Shoe>
            {
                new Shoe
                {
                    Name = "Air Max 90",
                    Brand = "Nike",
                    Description = "Klassieke Nike Air Max met zichtbare Air-unit voor optimaal comfort",
                    Price = 129.99m,
                    CategoryId = sneakersCat.Id,
                    Gender = "Unisex",
                    ImageUrl = "/images/nike-air-max.jpg",
                    IsActive = true,
                    Variants = new List<ShoeVariant>
                    {
                        new ShoeVariant { Size = 40, Color = "White/Red", Stock = 10, SKU = "NIKE-AM90-40-WR" },
                        new ShoeVariant { Size = 41, Color = "White/Red", Stock = 15, SKU = "NIKE-AM90-41-WR" },
                        new ShoeVariant { Size = 42, Color = "White/Red", Stock = 20, SKU = "NIKE-AM90-42-WR" },
                        new ShoeVariant { Size = 42, Color = "Black", Stock = 12, SKU = "NIKE-AM90-42-BK" }
                    }
                },
                new Shoe
                {
                    Name = "Classic Leather",
                    Brand = "Reebok",
                    Description = "Tijdloze leren sneaker met vintage uitstraling",
                    Price = 89.99m,
                    CategoryId = casualCat.Id,
                    Gender = "Unisex",
                    ImageUrl = "/images/reebok-classic.jpg",
                    IsActive = true,
                    Variants = new List<ShoeVariant>
                    {
                        new ShoeVariant { Size = 41, Color = "Black", Stock = 8, SKU = "RBK-CL-41-BK" },
                        new ShoeVariant { Size = 42, Color = "Black", Stock = 10, SKU = "RBK-CL-42-BK" },
                        new ShoeVariant { Size = 43, Color = "White", Stock = 5, SKU = "RBK-CL-43-WH" }
                    }
                },
                new Shoe
                {
                    Name = "Stan Smith",
                    Brand = "Adidas",
                    Description = "Iconische witte sneaker met groene accenten",
                    Price = 99.99m,
                    CategoryId = sneakersCat.Id,
                    Gender = "Unisex",
                    ImageUrl = "/images/adidas-stan-smith.jpg",
                    IsActive = true,
                    Variants = new List<ShoeVariant>
                    {
                        new ShoeVariant { Size = 39, Color = "Green/White", Stock = 7, SKU = "ADS-SS-39-GW" },
                        new ShoeVariant { Size = 40, Color = "Green/White", Stock = 12, SKU = "ADS-SS-40-GW" },
                        new ShoeVariant { Size = 41, Color = "Green/White", Stock = 15, SKU = "ADS-SS-41-GW" }
                    }
                },
                new Shoe
                {
                    Name = "Pegasus 40",
                    Brand = "Nike",
                    Description = "Veelzijdige hardloopschoen voor alle afstanden",
                    Price = 139.99m,
                    CategoryId = runningCat.Id,
                    Gender = "Male",
                    ImageUrl = "/images/nike-pegasus.jpg",
                    IsActive = true,
                    Variants = new List<ShoeVariant>
                    {
                        new ShoeVariant { Size = 42, Color = "Blue", Stock = 10, SKU = "NIKE-PEG40-42-BL" },
                        new ShoeVariant { Size = 43, Color = "Blue", Stock = 8, SKU = "NIKE-PEG40-43-BL" },
                        new ShoeVariant { Size = 44, Color = "Black", Stock = 6, SKU = "NIKE-PEG40-44-BK" }
                    }
                },
                new Shoe
                {
                    Name = "Ultraboost 22",
                    Brand = "Adidas",
                    Description = "Premium hardloopschoen met Boost demping",
                    Price = 179.99m,
                    CategoryId = runningCat.Id,
                    Gender = "Female",
                    ImageUrl = "/images/adidas-ultraboost.jpg",
                    IsActive = true,
                    Variants = new List<ShoeVariant>
                    {
                        new ShoeVariant { Size = 37, Color = "Pink", Stock = 5, SKU = "ADS-UB22-37-PK" },
                        new ShoeVariant { Size = 38, Color = "Pink", Stock = 8, SKU = "ADS-UB22-38-PK" },
                        new ShoeVariant { Size = 39, Color = "White", Stock = 10, SKU = "ADS-UB22-39-WH" }
                    }
                }
            };

            await _context.Shoes.AddRangeAsync(shoes);
            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ {shoes.Count} schoenen met varianten toegevoegd aan database");
        }
    }
}
