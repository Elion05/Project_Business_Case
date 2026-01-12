using System.ComponentModel.DataAnnotations;

namespace BestelApp_Web.Models
{
    /// <summary>
    /// Model voor het registratie formulier
    /// </summary>
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Voornaam is verplicht")]
        [Display(Name = "Voornaam")]
        [StringLength(30, ErrorMessage = "Voornaam mag maximaal 30 karakters zijn")]
        public string VoorNaam { get; set; } = string.Empty;

        [Required(ErrorMessage = "Achternaam is verplicht")]
        [Display(Name = "Achternaam")]
        [StringLength(30, ErrorMessage = "Achternaam mag maximaal 30 karakters zijn")]
        public string AchterNaam { get; set; } = string.Empty;

        [Required(ErrorMessage = "Gebruikersnaam is verplicht")]
        [Display(Name = "Gebruikersnaam")]
        [StringLength(50, ErrorMessage = "Gebruikersnaam mag maximaal 50 karakters zijn")]
        public string GebruikersNaam { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is verplicht")]
        [EmailAddress(ErrorMessage = "Ongeldig email adres")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Wachtwoord is verplicht")]
        [StringLength(100, ErrorMessage = "Het wachtwoord moet minimaal {2} en maximaal {1} karakters zijn.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Wachtwoord")]
        public string Wachtwoord { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Bevestig wachtwoord")]
        [Compare("Wachtwoord", ErrorMessage = "Wachtwoorden komen niet overeen.")]
        public string BevestigWachtwoord { get; set; } = string.Empty;
    }
}

