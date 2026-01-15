using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BestelApp_Models
{
    /// <summary>
    /// Item binnen een order
    /// Bevroren snapshot van CartItem op moment van bestelling
    /// </summary>
    public class OrderItem
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Order waar dit item bij hoort
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property naar Order
        /// </summary>
        [ForeignKey("OrderId")]
        public Order Order { get; set; } = null!;

        /// <summary>
        /// ShoeVariant ID (bevroren op moment van bestelling)
        /// Nullable voor backward compatibility (oude orders zonder variants)
        /// </summary>
        public long? ShoeVariantId { get; set; }

        /// <summary>
        /// Navigation property naar ShoeVariant
        /// </summary>
        [ForeignKey("ShoeVariantId")]
        public ShoeVariant? ShoeVariant { get; set; }

        /// <summary>
        /// Aantal besteld
        /// </summary>
        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }

        /// <summary>
        /// Prijs per stuk op moment van bestelling (bevroren)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceAtOrder { get; set; }

        /// <summary>
        /// Product naam op moment van bestelling (bevroren)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Merk op moment van bestelling (bevroren)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Brand { get; set; } = string.Empty;

        /// <summary>
        /// Maat (bevroren)
        /// </summary>
        [Required]
        public int Size { get; set; }

        /// <summary>
        /// Kleur (bevroren)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// Subtotaal voor dit item
        /// </summary>
        [NotMapped]
        public decimal SubTotal => Quantity * PriceAtOrder;

        public override string ToString()
        {
            return $"{Brand} {ProductName} - Size {Size} ({Color}) - {Quantity}x €{PriceAtOrder}";
        }
    }
}
