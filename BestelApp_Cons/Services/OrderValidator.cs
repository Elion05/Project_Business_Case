using BestelApp_Cons.Models;

namespace BestelApp_Cons.Services
{
    /// <summary>
    /// Validator voor Order messages
    /// Valideert verplichte velden en data integriteit
    /// </summary>
    public class OrderValidator
    {
        /// <summary>
        /// Valideer order message
        /// </summary>
        /// <returns>ValidationResult met IsValid en lijst van fouten</returns>
        public static ValidationResult ValidateOrder(OrderMessage order)
        {
            var result = new ValidationResult { IsValid = true };

            // Validatie 1: Order ID verplicht
            if (string.IsNullOrWhiteSpace(order.OrderId))
            {
                result.IsValid = false;
                result.Errors.Add("OrderId is verplicht");
            }

            // Validatie 2: User ID verplicht
            if (string.IsNullOrWhiteSpace(order.UserId))
            {
                result.IsValid = false;
                result.Errors.Add("UserId is verplicht");
            }

            // Validatie 3: User naam verplicht
            if (string.IsNullOrWhiteSpace(order.UserName))
            {
                result.IsValid = false;
                result.Errors.Add("UserName is verplicht");
            }

            // Validatie 4: User email verplicht en geldig formaat
            if (string.IsNullOrWhiteSpace(order.UserEmail))
            {
                result.IsValid = false;
                result.Errors.Add("UserEmail is verplicht");
            }
            else if (!IsValidEmail(order.UserEmail))
            {
                result.IsValid = false;
                result.Errors.Add($"UserEmail heeft ongeldig formaat: {order.UserEmail}");
            }

            // Validatie 5: Items verplicht en niet leeg
            if (order.Items == null || order.Items.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add("Order moet minimaal 1 item bevatten");
            }
            else
            {
                // Valideer elk item
                for (int i = 0; i < order.Items.Count; i++)
                {
                    var item = order.Items[i];

                    if (string.IsNullOrWhiteSpace(item.ProductName))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Item {i + 1}: ProductName is verplicht");
                    }

                    if (string.IsNullOrWhiteSpace(item.Brand))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Item {i + 1}: Brand is verplicht");
                    }

                    if (item.Size <= 0)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Item {i + 1}: Size moet groter dan 0 zijn");
                    }

                    if (string.IsNullOrWhiteSpace(item.Color))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Item {i + 1}: Color is verplicht");
                    }

                    if (item.Quantity <= 0)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Item {i + 1}: Quantity moet groter dan 0 zijn");
                    }

                    if (item.Price <= 0)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Item {i + 1}: Price moet groter dan 0 zijn");
                    }
                }
            }

            // Validatie 6: TotalPrice moet groter dan 0 zijn
            if (order.TotalPrice <= 0)
            {
                result.IsValid = false;
                result.Errors.Add("TotalPrice moet groter dan 0 zijn");
            }

            // Validatie 7: TotalQuantity moet groter dan 0 zijn
            if (order.TotalQuantity <= 0)
            {
                result.IsValid = false;
                result.Errors.Add("TotalQuantity moet groter dan 0 zijn");
            }

            // Validatie 8: ShippingAddress verplicht
            if (order.ShippingAddress == null)
            {
                result.IsValid = false;
                result.Errors.Add("ShippingAddress is verplicht");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(order.ShippingAddress.Address))
                {
                    result.IsValid = false;
                    result.Errors.Add("ShippingAddress.Address is verplicht");
                }

                if (string.IsNullOrWhiteSpace(order.ShippingAddress.City))
                {
                    result.IsValid = false;
                    result.Errors.Add("ShippingAddress.City is verplicht");
                }

                if (string.IsNullOrWhiteSpace(order.ShippingAddress.PostalCode))
                {
                    result.IsValid = false;
                    result.Errors.Add("ShippingAddress.PostalCode is verplicht");
                }

                if (string.IsNullOrWhiteSpace(order.ShippingAddress.Country))
                {
                    result.IsValid = false;
                    result.Errors.Add("ShippingAddress.Country is verplicht");
                }
            }

            // Validatie 9: OrderDate moet in het verleden of heden zijn
            if (order.OrderDate > DateTime.UtcNow.AddMinutes(5)) // 5 min marge voor clock skew
            {
                result.IsValid = false;
                result.Errors.Add($"OrderDate ligt in de toekomst: {order.OrderDate}");
            }

            // Validatie 10: Status verplicht
            if (string.IsNullOrWhiteSpace(order.Status))
            {
                result.IsValid = false;
                result.Errors.Add("Status is verplicht");
            }

            return result;
        }

        /// <summary>
        /// Simpele email validatie - soepelere validatie voor test emails
        /// </summary>
        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            // Basis validatie: moet @ bevatten en minstens 1 karakter voor en na @
            if (!email.Contains("@"))
                return false;

            var parts = email.Split('@');
            if (parts.Length != 2)
                return false;

            if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                return false;

            // Als het een test email is zonder TLD (bijv. "test@gmail"), accepteren we het
            // Dit is soepeler dan MailAddress validatie
            return true;
        }
    }

    /// <summary>
    /// Resultaat van validatie
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();

        public override string ToString()
        {
            if (IsValid)
            {
                return "✓ Validatie succesvol";
            }
            else
            {
                return $"✗ Validatie gefaald:\n  - " + string.Join("\n  - ", Errors);
            }
        }
    }
}
