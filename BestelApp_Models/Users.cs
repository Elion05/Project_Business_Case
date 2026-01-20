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

        // Adresgegevens
        [MaxLength(100)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(50)]
        public string City { get; set; } = string.Empty;

        [MaxLength(10)]
        public string PostalCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Country { get; set; } = string.Empty;




        public override string ToString()
        {
            return $"{FirstName} {LastName} ({UserName})";
        }

        /// <summary>
        /// Volledig adres als string
        /// </summary>
        public string FullAddress =>
            string.IsNullOrEmpty(Address)
                ? "Geen adres opgegeven"
                : $"{Address}, {PostalCode} {City}, {Country}";

    }
}
