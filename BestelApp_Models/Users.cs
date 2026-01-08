using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;


namespace BestelApp_Models
{
    public class Users : IdentityUser
    {


        [Required]
        [MaxLength(30)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string LastName { get; set; } = string.Empty;




        public static Users Dummy = new Users
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = "-",
            LastName = "-",
            UserName = "dummy",
            NormalizedUserName = "DUMMY",
            Email = "dummy@manga.be",
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.MaxValue,
        };


        public override string ToString()
        {
            return $"{FirstName} {LastName} ({UserName})";
        }


        public static async Task Seeder()
        {
            DbContext context = new DbContext();

            if (!context.Roles.Any())
            {
                context.Roles.AddRange(new List<IdentityRole>
                {
                    new IdentityRole { Id = "Admin", Name = "Admin", NormalizedName = "ADMIN" },
                    new IdentityRole { Id = "User", Name = "User", NormalizedName = "USER" },
                    new IdentityRole {Id = "System_Admin", Name = "System_Admin", NormalizedName = "SYSTEM_ADMIN" }
                });

                context.SaveChanges();
            }

            if (!context.Users.Any())
            {
                context.Add(Dummy);
                context.SaveChanges();

                var admin = new Users
                {
                    UserName = "admin",
                    FirstName = "Admin",
                    LastName = "User",
                    Email = "admin@manga.be",
                    EmailConfirmed = true,
                };


                var normaleUser = new Users
                {
                    UserName = "user",
                    FirstName = "Normal",
                    LastName = "User",
                    Email = "user@manga.be",
                    EmailConfirmed = true,
                };


                var systeemAdmin = new Users
                {
                    UserName = "system_admin",
                    FirstName = "System",
                    LastName = "Admin",
                    Email = "system_admin@manga.be",
                    EmailConfirmed = true,
                };



                var usermanager = new UserManager<Users>(new UserStore<Users>(context), null, new PasswordHasher<Users>(),
                    null, null, null, null, null, null
                );

                try
                {
                    await usermanager.CreateAsync(admin, "Admin123!");
                    await usermanager.CreateAsync(normaleUser, "User123!");
                    await usermanager.CreateAsync(systeemAdmin, "SystemAdmin123!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating users: {ex.Message}");
                }

                while(context.Users.Count() < 3)
                {
                    await Task.Delay(500);
                }

                await usermanager.AddToRoleAsync(admin, "Admin");
                await usermanager.AddToRoleAsync(normaleUser, "User");
                await usermanager.AddToRoleAsync(systeemAdmin, "System_Admin");
            }

            Dummy = context.Users.First(u => u.UserName == "dummy");
        }
    }
}
