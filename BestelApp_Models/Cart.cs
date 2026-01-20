using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BestelApp_Models
{
    /// <summary>
    /// Shopping Cart per user
    /// Elke user heeft 1 actieve cart
    /// </summary>
    public class Cart
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// User die eigenaar is van deze cart
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property naar User
        /// </summary>
        [ForeignKey("UserId")]
        public Users User { get; set; } = null!;

        /// <summary>
        /// Items in de cart
        /// </summary>
        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();

        /// <summary>
        /// Wanneer cart is aangemaakt
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Laatst gewijzigd
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Totaal aantal items in cart
        /// </summary>
        [NotMapped]
        public int TotalItems => Items.Sum(i => i.Quantity);

        /// <summary>
        /// Totale prijs van alle items
        /// </summary>
        [NotMapped]
        public decimal TotalPrice => Items.Sum(i => i.Quantity * i.Price);

        public override string ToString()
        {
            return $"Cart voor {User?.UserName} - {TotalItems} items - €{TotalPrice}";
        }
    }
}
