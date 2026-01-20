using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BestelApp_Models
{
    /// <summary>
    /// Shoe model - Basis product informatie
    /// Elke schoen kan meerdere varianten hebben (ShoeVariant)
    /// </summary>
    public class Shoe
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Brand { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Basis prijs (kan per variant verschillen)
        /// </summary>
        [Required]
        [Range(0.01, 10000)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        /// <summary>
        /// Foreign Key naar Category
        /// </summary>
        [Required]
        public long CategoryId { get; set; }

        /// <summary>
        /// Navigation property naar Category
        /// </summary>
        [ForeignKey("CategoryId")]
        public Category Category { get; set; } = null!;

        /// <summary>
        /// Gender: "Male", "Female", "Unisex"
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Gender { get; set; } = "Unisex";

        /// <summary>
        /// Product afbeelding URL
        /// </summary>
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Is product actief/beschikbaar?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Wanneer product is toegevoegd
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property: Varianten van deze schoen (maat, kleur, voorraad)
        /// </summary>
        public ICollection<ShoeVariant> Variants { get; set; } = new List<ShoeVariant>();

        /// <summary>
        /// DEPRECATED: Oude velden voor backwards compatibility
        /// Gebruik Variants voor voorraad per maat/kleur
        /// </summary>
        [Range(20, 50)]
        public int? Size { get; set; }

        [MaxLength(100)]
        public string? Color { get; set; }

        public override string ToString()
        {
            return $"{Brand} {Name} - €{Price}";
        }
    }
}

