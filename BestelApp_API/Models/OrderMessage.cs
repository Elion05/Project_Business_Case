using System.Text.Json.Serialization;

namespace BestelApp_API.Models
{
    /// <summary>
    /// Order message DTO voor RabbitMQ
    /// Bevat alle informatie die naar de queue gestuurd wordt
    /// </summary>
    public class OrderMessage
    {
        /// <summary>
        /// Unieke Order ID (bijv. ORDER-20260113133045-123)
        /// </summary>
        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// User ID van klant
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gebruikersnaam
        /// </summary>
        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Email van klant
        /// </summary>
        [JsonPropertyName("userEmail")]
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// Items in de bestelling
        /// </summary>
        [JsonPropertyName("items")]
        public List<OrderItemMessage> Items { get; set; } = new();

        /// <summary>
        /// Totale prijs
        /// </summary>
        [JsonPropertyName("totalPrice")]
        public decimal TotalPrice { get; set; }

        /// <summary>
        /// Totaal aantal items
        /// </summary>
        [JsonPropertyName("totalQuantity")]
        public int TotalQuantity { get; set; }

        /// <summary>
        /// Verzendadres
        /// </summary>
        [JsonPropertyName("shippingAddress")]
        public ShippingAddressMessage ShippingAddress { get; set; } = new();

        /// <summary>
        /// Wanneer bestelling is geplaatst
        /// </summary>
        [JsonPropertyName("orderDate")]
        public DateTime OrderDate { get; set; }

        /// <summary>
        /// Order status
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Notities bij bestelling
        /// </summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Wanneer message is aangemaakt
        /// </summary>
        [JsonPropertyName("messageCreatedAt")]
        public DateTime MessageCreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Order item binnen een order message
    /// </summary>
    public class OrderItemMessage
    {
        [JsonPropertyName("productName")]
        public string ProductName { get; set; } = string.Empty;
        
        [JsonPropertyName("brand")]
        public string Brand { get; set; } = string.Empty;
        
        [JsonPropertyName("size")]
        public int Size { get; set; }
        
        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;
        
        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
        
        [JsonPropertyName("price")]
        public decimal Price { get; set; }
        
        [JsonPropertyName("subTotal")]
        public decimal SubTotal { get; set; }
    }

    /// <summary>
    /// Verzendadres binnen een order message
    /// </summary>
    public class ShippingAddressMessage
    {
        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;
        
        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;
        
        [JsonPropertyName("postalCode")]
        public string PostalCode { get; set; } = string.Empty;
        
        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;
        
        [JsonPropertyName("fullAddress")]
        public string FullAddress { get; set; } = string.Empty;
    }
}
