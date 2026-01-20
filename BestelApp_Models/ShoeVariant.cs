using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BestelApp_Models
{
    /// <summary>
    /// ShoeVariant model voor verschillende varianten van een schoen
    /// Elke schoen kan meerdere varianten hebben (maat, kleur, voorraad)
    /// </summary>
    public class ShoeVariant
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Foreign Key naar Shoe
        /// </summary>
        [Required]
        public long ShoeId { get; set; }

        /// <summary>
        /// Navigation property naar Shoe
        /// </summary>
        [ForeignKey("ShoeId")]
        public Shoe Shoe { get; set; } = null!;

        /// <summary>
        /// Maat (EU sizing)
        /// </summary>
        [Required]
        [Range(20, 50)]
        public int Size { get; set; }

        /// <summary>
        /// Kleur van deze variant
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// Voorraad aantal
        /// </summary>
        [Required]
        [Range(0, 10000)]
        public int Stock { get; set; } = 0;

        /// <summary>
        /// SKU (Stock Keeping Unit) - unieke code
        /// </summary>
        [MaxLength(50)]
        public string SKU { get; set; } = string.Empty;

        /// <summary>
        /// Is deze variant beschikbaar voor bestelling?
        /// </summary>
        public bool IsAvailable => Stock > 0;

        public override string ToString()
        {
            return $"{Shoe?.Brand} {Shoe?.Name} - Size {Size} - {Color} (Stock: {Stock})";
        }
    }
}
