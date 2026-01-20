using System.Text.Json.Serialization;

namespace BestelApp_Cons.Models
{
    /// <summary>
    /// Order message van RabbitMQ
    /// Komt overeen met OrderMessage van BestelApp_API
    /// </summary>
    public class OrderMessage
    {
        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("userEmail")]
        public string UserEmail { get; set; } = string.Empty;

        [JsonPropertyName("items")]
        public List<OrderItemMessage> Items { get; set; } = new();

        [JsonPropertyName("totalPrice")]
        public decimal TotalPrice { get; set; }

        [JsonPropertyName("totalQuantity")]
        public int TotalQuantity { get; set; }

        [JsonPropertyName("shippingAddress")]
        public ShippingAddressMessage? ShippingAddress { get; set; }

        [JsonPropertyName("orderDate")]
        public DateTime OrderDate { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;

        [JsonPropertyName("messageCreatedAt")]
        public DateTime MessageCreatedAt { get; set; }
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
