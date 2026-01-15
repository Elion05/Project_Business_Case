using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BestelApp_Models
{
    /// <summary>
    /// Order model - Definitieve bestelling na checkout
    /// Bevat meerdere OrderItems (producten)
    /// </summary>
    public class Order
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Unieke order ID (bijv. ORDER-20260113133045-123)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// User die de bestelling heeft geplaatst
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property naar User
        /// </summary>
        [ForeignKey("UserId")]
        public Users User { get; set; } = null!;

        /// <summary>
        /// Items in deze order
        /// </summary>
        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

        /// <summary>
        /// Totale prijs (som van alle items)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        /// <summary>
        /// Order status: Pending, Processing, Completed, Cancelled, Failed
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Wanneer bestelling is geplaatst
        /// </summary>
        [Required]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Is naar RabbitMQ gestuurd?
        /// </summary>
        public bool IsSentToQueue { get; set; } = false;

        /// <summary>
        /// Wanneer naar queue gestuurd
        /// </summary>
        public DateTime? SentToQueueAt { get; set; }

        /// <summary>
        /// Verzendadres (snapshot van user adres op moment van bestelling)
        /// </summary>
        [MaxLength(100)]
        public string ShippingAddress { get; set; } = string.Empty;

        [MaxLength(50)]
        public string ShippingCity { get; set; } = string.Empty;

        [MaxLength(10)]
        public string ShippingPostalCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string ShippingCountry { get; set; } = string.Empty;

        /// <summary>
        /// Notities bij bestelling
        /// </summary>
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Totaal aantal items in order
        /// </summary>
        [NotMapped]
        public int TotalQuantity => Items.Sum(i => i.Quantity);

        public override string ToString()
        {
            return $"Order {OrderId} - {TotalQuantity} items - €{TotalPrice} - {Status}";
        }
    }
}

