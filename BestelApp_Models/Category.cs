using System.ComponentModel.DataAnnotations;

namespace BestelApp_Models
{
    /// <summary>
    /// Category model voor schoen categorieën
    /// Bijvoorbeeld: Sport, Casual, Formal, etc.
    /// </summary>
    public class Category
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Is deze categorie actief?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Navigation property: Schoenen in deze categorie
        /// </summary>
        public ICollection<Shoe> Shoes { get; set; } = new List<Shoe>();

        public override string ToString()
        {
            return Name;
        }
    }
}
