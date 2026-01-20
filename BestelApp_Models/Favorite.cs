using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BestelApp_Models
{
    /// <summary>
    /// Favorite model voor gebruikers om producten te favorieten
    /// Elke gebruiker kan een product maar 1x favorieten
    /// </summary>
    public class Favorite
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// User die dit product heeft gefavoriet
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property naar User
        /// </summary>
        [ForeignKey("UserId")]
        public Users User { get; set; } = null!;

        /// <summary>
        /// Product dat is gefavoriet
        /// </summary>
        [Required]
        public long ShoeId { get; set; }

        /// <summary>
        /// Navigation property naar Shoe
        /// </summary>
        [ForeignKey("ShoeId")]
        public Shoe Shoe { get; set; } = null!;

        /// <summary>
        /// Wanneer toegevoegd aan favorieten
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
