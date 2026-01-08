using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace BestelApp_Models
{

    public class DbContext : IdentityDbContext<Users>
    {

        public DbSet<Users> Users { get; set; }





        public static async Task Seeder(DbContext context)
        {

        }
    }
//}