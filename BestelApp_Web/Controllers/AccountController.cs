using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BestelApp_Models;
using BestelApp_Web.Models;

namespace BestelApp_Web.Controllers
{
    /// <summary>
    /// Controller voor login, register en logout functionaliteit
    /// </summary>
    public class AccountController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly SignInManager<Users> _signInManager;

        // Constructor - hier krijgen we de managers binnen
        public AccountController(UserManager<Users> userManager, SignInManager<Users> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // ========================================
        // REGISTER (Registreren)
        // ========================================

        /// <summary>
        /// GET: Toon registratie formulier
        /// </summary>
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        /// <summary>
        /// POST: Verwerk registratie
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            // Check of alle velden correct zijn ingevuld
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Maak een nieuwe gebruiker aan
            var nieuweGebruiker = new Users
            {
                UserName = model.GebruikersNaam,
                Email = model.Email,
                FirstName = model.VoorNaam,
                LastName = model.AchterNaam,
                EmailConfirmed = true // Voor development meteen bevestigd
            };

            // Probeer de gebruiker aan te maken in de database
            var resultaat = await _userManager.CreateAsync(nieuweGebruiker, model.Wachtwoord);

            if (resultaat.Succeeded)
            {
                // Gelukt! Voeg de gebruiker toe aan de "User" rol
                await _userManager.AddToRoleAsync(nieuweGebruiker, "User");

                // Log de gebruiker meteen in
                await _signInManager.SignInAsync(nieuweGebruiker, isPersistent: false);

                // Ga naar de home pagina
                TempData["SuccessBericht"] = $"Welkom {nieuweGebruiker.FirstName}! Je account is aangemaakt.";
                return RedirectToAction("Index", "Home");
            }

            // Er ging iets mis, toon de fouten
            foreach (var fout in resultaat.Errors)
            {
                ModelState.AddModelError(string.Empty, fout.Description);
            }

            return View(model);
        }

        // ========================================
        // LOGIN (Inloggen)
        // ========================================

        /// <summary>
        /// GET: Toon login formulier
        /// </summary>
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        /// <summary>
        /// POST: Verwerk login
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Check of alle velden correct zijn ingevuld
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Probeer in te loggen
            var resultaat = await _signInManager.PasswordSignInAsync(
                model.GebruikersNaam,
                model.Wachtwoord,
                model.OnthoudMij,
                lockoutOnFailure: false // Voor development geen lockout
            );

            if (resultaat.Succeeded)
            {
                // Gelukt! Haal de gebruiker op
                var gebruiker = await _userManager.FindByNameAsync(model.GebruikersNaam);
                TempData["SuccessBericht"] = $"Welkom terug {gebruiker?.FirstName}!";

                // Ga terug naar waar de gebruiker vandaan kwam, of naar home
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
            }

            // Login mislukt
            ModelState.AddModelError(string.Empty, "Ongeldige gebruikersnaam of wachtwoord.");
            return View(model);
        }

        // ========================================
        // LOGOUT (Uitloggen)
        // ========================================

        /// <summary>
        /// POST: Uitloggen
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            TempData["SuccessBericht"] = "Je bent uitgelogd.";
            return RedirectToAction("Index", "Home");
        }

        // ========================================
        // ACCESS DENIED (Geen toegang)
        // ========================================

        /// <summary>
        /// GET: Toon "geen toegang" pagina
        /// </summary>
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}

