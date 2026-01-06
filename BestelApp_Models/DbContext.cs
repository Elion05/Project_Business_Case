using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace BestelApp_Models
{

    public class DbContext : IdentityDbContext<Users>
    {

    }
}