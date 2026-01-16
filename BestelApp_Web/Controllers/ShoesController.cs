using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BestelApp_Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace BestelApp_Web.Controllers
{
    [Authorize] // Alle acties vereisen ingelogde gebruiker
    public class ShoesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BestelApp_Web.Services.OrderApiService _orderApiService;

        public ShoesController(ApplicationDbContext context, BestelApp_Web.Services.OrderApiService orderApiService)
        {
            _context = context;
            _orderApiService = orderApiService;
        }

        //dit is een property voor de Shoes 
        private DbSet<Shoe> Shoes => _context.Set<Shoe>();

        // GET: Shoes
        public async Task<IActionResult> Index()
        {
            return View(await Shoes.ToListAsync());
        }

        // GET: Shoes/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shoe = await Shoes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (shoe == null)
            {
                return NotFound();
            }

            return View(shoe);
        }

        // GET: Shoes/Create
        [Authorize(Roles = "Admin")] // Alleen Admin mag schoenen toevoegen
        public IActionResult Create()
        {
            return View();
        }

        // POST: Shoes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")] // Alleen Admin mag schoenen toevoegen
        public async Task<IActionResult> Create(Shoe shoe)
        {
            // DEBUG: Log wat er binnenkomt
            Console.WriteLine($"🔍 DEBUG Create Shoe:");
            Console.WriteLine($"  - CategoryId: {shoe.CategoryId}");
            Console.WriteLine($"  - Name: {shoe.Name}");
            Console.WriteLine($"  - Brand: {shoe.Brand}");
            Console.WriteLine($"  - Description: {shoe.Description}");

            // DEBUG: Check ModelState errors
            if (!ModelState.IsValid)
            {
                Console.WriteLine($"⚠️ ModelState INVALID! Errors:");
                foreach (var error in ModelState)
                {
                    if (error.Value.Errors.Count > 0)
                    {
                        Console.WriteLine($"  - {error.Key}: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                    }
                }
            }

            // Verwijder de Category navigation property validatie error
            // We sturen alleen CategoryId, niet het hele Category object
            ModelState.Remove("Category");

            // Als CategoryId 0 is, probeer de eerste category te pakken
            if (shoe.CategoryId == 0)
            {
                var firstCategory = await _context.Set<Category>().FirstOrDefaultAsync();
                if (firstCategory != null)
                {
                    Console.WriteLine($"⚠️ CategoryId was 0, zet naar eerste category: {firstCategory.Id} ({firstCategory.Name})");
                    shoe.CategoryId = firstCategory.Id;
                    ModelState.Remove("CategoryId"); // Remove error
                }
                else
                {
                    Console.WriteLine($"❌ GEEN CATEGORIES GEVONDEN IN DATABASE!");
                    ModelState.AddModelError("CategoryId", "Geen categorieën beschikbaar. Voeg eerst categorieën toe aan de database.");
                }
            }
            else
            {
                Console.WriteLine($"✅ CategoryId ontvangen: {shoe.CategoryId}");
            }

            if (ModelState.IsValid)
            {
                // Zet CreatedAt timestamp
                shoe.CreatedAt = DateTime.UtcNow;

                _context.Add(shoe);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Schoen succesvol toegevoegd!");
                TempData["SuccessMessage"] = $"Schoen '{shoe.Name}' succesvol toegevoegd!";
                return RedirectToAction(nameof(Index));
            }

            // Als validatie faalt, toon fouten
            Console.WriteLine($"❌ Validatie gefaald, terug naar formulier");
            return View(shoe);
        }

        // GET: Shoes/Edit/5
        [Authorize(Roles = "Admin")] // Alleen Admin mag schoenen bewerken
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shoe = await Shoes.FindAsync(id);
            if (shoe == null)
            {
                return NotFound();
            }
            return View(shoe);
        }

        // POST: Shoes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")] // Alleen Admin mag schoenen bewerken
        public async Task<IActionResult> Edit(long id, Shoe shoe)
        {
            if (id != shoe.Id)
            {
                return NotFound();
            }

            // Verwijder de Category navigation property validatie error
            // We sturen alleen CategoryId, niet het hele Category object
            ModelState.Remove("Category");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(shoe);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Schoen '{shoe.Name}' succesvol bijgewerkt!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShoeExists(shoe.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(shoe);
        }

        // GET: Shoes/Delete/5
        [Authorize(Roles = "Admin")] // Alleen Admin mag schoenen verwijderen
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shoe = await Shoes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (shoe == null)
            {
                return NotFound();
            }

            return View(shoe);
        }

        // POST: Shoes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")] // Alleen Admin mag schoenen verwijderen
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var shoe = await Shoes.FindAsync(id);
            if (shoe != null)
            {
                Shoes.Remove(shoe);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ShoeExists(long id)
        {
            return Shoes.Any(e => e.Id == id);
        }


        // POST: Shoes/Order/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Order(long id)
        {
            var shoe = await Shoes.FindAsync(id);
            if (shoe == null)
            {
                return NotFound();
            }

            // Haal ingelogde user ID op
            var gebruikerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(gebruikerId))
            {
                TempData["ErrorMessage"] = "Je moet ingelogd zijn om te bestellen.";
                return RedirectToAction("Login", "Account");
            }

            // Verstuur naar Backend API (niet meer direct naar RabbitMQ!)
            var success = await _orderApiService.PlaceOrderAsync(shoe, gebruikerId);

            if (success)
            {
                //Dit is een kleine message dat je op de schoenen Index te zien krijgt
                TempData["SuccessMessage"] = $"Bestelling voor {shoe.Name} is verzonden!";
            }
            else
            {
                TempData["ErrorMessage"] = $"Fout bij versturen bestelling. Probeer later opnieuw.";
            }
            return RedirectToAction(nameof(Index));
        }


    }
}
