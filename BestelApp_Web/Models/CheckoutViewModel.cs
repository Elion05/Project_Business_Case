using BestelApp_Web.Services;

namespace BestelApp_Web.Models
{
    public class CheckoutViewModel
    {
        public CartApiService.CartResponse? Cart { get; set; }

        // Adresgegevens
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        // Persoonlijke info (read-only in UI)
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }

    public class CheckoutResponse
    {
        public string OrderId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}

