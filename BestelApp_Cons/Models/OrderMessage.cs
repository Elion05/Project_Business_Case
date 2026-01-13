using System;
using System.Text.Json.Serialization;

namespace BestelApp_Cons.Models
{
    /// <summary>
    /// Model voor JSON berichten die van RabbitMQ komen
    /// Dit is de structuur van een bestelling
    /// </summary>
    public class OrderMessage
    {
        /// <summary>
        /// Unieke ID van de bestelling
        /// </summary>
        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// Merk van de schoen (bijv. "Nike", "Adidas")
        /// </summary>
        [JsonPropertyName("brand")]
        public string Brand { get; set; } = string.Empty;

        /// <summary>
        /// Naam van de schoen (bijv. "Air Max")
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Maat van de schoen (bijv. 42)
        /// </summary>
        [JsonPropertyName("size")]
        public int Size { get; set; }

        /// <summary>
        /// Prijs van de schoen (bijv. 99.99)
        /// </summary>
        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        /// <summary>
        /// Wanneer de bestelling is aangemaakt
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}
