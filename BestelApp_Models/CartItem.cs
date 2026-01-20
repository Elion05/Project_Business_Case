using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BestelApp_Models
{
    /// <summary>
    /// Item in een shopping cart
    /// Gekoppeld aan een specifieke ShoeVariant (maat/kleur)
    /// </summary>
    public class CartItem
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Cart waar dit item bij hoort
        /// </summary>
        [Required]
        public long CartId { get; set; }

        /// <summary>
        /// Navigation property naar Cart
        /// </summary>
        [ForeignKey("CartId")]
        public Cart Cart { get; set; } = null!;

        /// <summary>
        /// Schoen variant (specifieke maat/kleur)
        /// </summary>
        [Required]
        public long ShoeVariantId { get; set; }

        /// <summary>
        /// Navigation property naar ShoeVariant
        /// </summary>
        [ForeignKey("ShoeVariantId")]
        public ShoeVariant ShoeVariant { get; set; } = null!;

        /// <summary>
        /// Aantal van dit item
        /// </summary>
        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Prijs per stuk (opgeslagen om prijswijzigingen te tracken)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        /// <summary>
        /// Wanneer toegevoegd aan cart
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Subtotaal voor dit item
        /// </summary>
        [NotMapped]
        public decimal SubTotal => Quantity * Price;

        public override string ToString()
        {
            return $"{ShoeVariant?.Shoe?.Brand} {ShoeVariant?.Shoe?.Name} - Size {ShoeVariant?.Size} - {Quantity}x €{Price}";
        }
    }
}
