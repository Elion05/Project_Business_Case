using System.Linq;
using System.Security.Claims;
using BestelApp_Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;


namespace BestelApp_Web.Controllers
{
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
        public async Task<IActionResult> Index(string? search, long? categoryId, string? gender, string? sortBy)
        {
            var query = Shoes.Include(s => s.Category).AsQueryable();

            // Categorieën voor filter UI
            ViewBag.Categorieen = await _context.Categories
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            // Zoekfilter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s =>
                    s.Name.Contains(search) ||
                    s.Brand.Contains(search) ||
                    s.Description.Contains(search));
            }

            // Categoriefilter
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(s => s.CategoryId == categoryId.Value);
            }

            // Gender filter
            if (!string.IsNullOrEmpty(gender) && gender != "Alles")
            {
                query = query.Where(s => s.Gender == gender);
            }

            // Sorteren
            switch (sortBy?.ToLower())
            {
                case "prijs-laag":
                    query = query.OrderBy(s => s.Price);
                    break;
                case "prijs-hoog":
                    query = query.OrderByDescending(s => s.Price);
                    break;
                case "naam-az":
                    query = query.OrderBy(s => s.Name);
                    break;
                case "naam-za":
                    query = query.OrderByDescending(s => s.Name);
                    break;
                case "nieuwste":
                    query = query.OrderByDescending(s => s.CreatedAt);
                    break;
                case "oudste":
                    query = query.OrderBy(s => s.CreatedAt);
                    break;
                default:
                    query = query.OrderByDescending(s => s.CreatedAt); // Standaard: nieuwste eerst
                    break;
            }

            // ViewBag voor filters
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.Gender = gender ?? "Alles";
            ViewBag.SortBy = sortBy ?? "nieuwste";

            return View(await query.ToListAsync());
        }

        // GET: Shoes/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shoe = await Shoes
                .Include(s => s.Variants)
                .Include(s => s.Category)
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

            // Verwijder navigation property validatie errors (we gebruiken alleen IDs)
            ModelState.Remove("Category");
            ModelState.Remove("Variants");

            // Verwijder oude Size en Color velden (die zijn niet meer nodig)
            ModelState.Remove("Size");
            ModelState.Remove("Color");

            // Verwijder alle variant-gerelateerde ModelState errors (we verwerken ze handmatig)
            var keysToRemove = ModelState.Keys.Where(k => k.StartsWith("Variants[") || k == "Shoe").ToList();
            foreach (var key in keysToRemove)
            {
                ModelState.Remove(key);
                Console.WriteLine($"🗑️ ModelState key verwijderd: {key}");
            }

            Console.WriteLine($"🔍 ModelState.IsValid na cleanup: {ModelState.IsValid}");

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

            // Forceer ModelState.IsValid door alle resterende errors te verwijderen
            // We verwerken varianten handmatig, dus we hebben ModelState niet nodig
            var allKeys = ModelState.Keys.ToList();
            foreach (var key in allKeys)
            {
                if (key.StartsWith("Variants[") || key == "Shoe" || key == "Category")
                {
                    ModelState.Remove(key);
                }
            }

            Console.WriteLine($"🔍 ModelState.IsValid na volledige cleanup: {ModelState.IsValid}");

            // Valideer handmatig de basisvelden
            if (string.IsNullOrWhiteSpace(shoe.Name) ||
                string.IsNullOrWhiteSpace(shoe.Brand) ||
                string.IsNullOrWhiteSpace(shoe.Description) ||
                shoe.CategoryId == 0 ||
                shoe.Price <= 0)
            {
                ModelState.AddModelError("", "Vul alle verplichte velden in");
                return View(shoe);
            }

            // VERWERK VARIANTEN EERST (VOORDAT we Shoe opslaan)
            // Dit moet gebeuren VOORDAT Model Binding de Request body heeft gelezen
            var variants = new List<ShoeVariant>();

            // Check of Request.Form beschikbaar is
            if (!Request.HasFormContentType)
            {
                Console.WriteLine($"❌ Request heeft geen form content type!");
                ModelState.AddModelError("", "Ongeldige form data");
                return View(shoe);
            }

            var form = Request.Form;

            Console.WriteLine($"🔍 Form keys: {string.Join(", ", form.Keys)}");
            Console.WriteLine($"🔍 Form keys count: {form.Keys.Count}");

            // Lees variant data uit form
            var variantIndices = new List<int>();
            Console.WriteLine($"🔍 Beginnen met zoeken naar variant indices...");
            foreach (var key in form.Keys)
            {
                Console.WriteLine($"🔍 Checking key: {key}");
                if (key.StartsWith("Variants[") && key.Contains("].Size"))
                {
                    var indexStr = key.Substring("Variants[".Length, key.IndexOf("]") - "Variants[".Length);
                    Console.WriteLine($"🔍 Found Variants[ key, indexStr: {indexStr}");
                    if (int.TryParse(indexStr, out int index))
                    {
                        variantIndices.Add(index);
                        Console.WriteLine($"✅ Variant index gevonden: {index}");
                    }
                }
            }

            Console.WriteLine($"🔍 Totaal variant indices: {variantIndices.Count}");

            if (variantIndices.Count == 0)
            {
                Console.WriteLine($"⚠️ GEEN VARIANT INDICES GEVONDEN! Alle form keys:");
                foreach (var key in form.Keys)
                {
                    Console.WriteLine($"  - {key} = {form[key]}");
                }
                // Valideer dat er minimaal 1 variant is VOORDAT we Shoe opslaan
                ModelState.AddModelError("", "Voeg minimaal één variant toe (maat, kleur, voorraad)");
                return View(shoe);
            }

            foreach (var index in variantIndices)
            {
                // Lees form waarden direct uit Request.Form
                var sizeStr = form[$"Variants[{index}].Size"].ToString() ?? "";
                var color = form[$"Variants[{index}].Color"].ToString() ?? "";
                var stockStr = form[$"Variants[{index}].Stock"].ToString() ?? "";

                Console.WriteLine($"🔍 Variant {index}: Size={sizeStr}, Color={color}, Stock={stockStr}");

                if (int.TryParse(sizeStr, out int size) &&
                    int.TryParse(stockStr, out int stock) &&
                    !string.IsNullOrWhiteSpace(color))
                {
                    // Genereer SKU automatisch - altijd gebruiken helper functie
                    // Format: BRAND-NAME-SIZE-COLOR (bijv. NIKE-AIRMAX-42-ZWART)
                    string sku = GenerateSku(shoe, size, color, index);
                    Console.WriteLine($"✅ SKU automatisch gegenereerd: {sku}");

                    // Zorg ervoor dat SKU altijd een niet-lege waarde heeft (database NOT NULL constraint)
                    if (string.IsNullOrWhiteSpace(sku))
                    {
                        // Gebruik tijdelijke ID (0) voor fallback SKU, wordt later geüpdatet
                        sku = $"SKU-TEMP-{index}-{size}-{DateTime.UtcNow.Ticks}";
                        Console.WriteLine($"⚠️ SKU was nog steeds leeg, laatste fallback: {sku}");
                    }

                    // Maak variant aan met ShoeId = 0 (wordt later gezet na SaveChangesAsync)
                    var variant = new ShoeVariant
                    {
                        ShoeId = 0, // Wordt later gezet na SaveChangesAsync
                        Size = size,
                        Color = color.Trim(),
                        Stock = stock,
                        SKU = sku // Altijd een niet-lege waarde (NOT NULL constraint)
                    };

                    Console.WriteLine($"✅ Variant aangemaakt: Size={variant.Size}, Color={variant.Color}, Stock={variant.Stock}, SKU='{variant.SKU}' (length={variant.SKU.Length})");
                    variants.Add(variant);
                }
                else
                {
                    Console.WriteLine($"❌ Variant {index} validatie gefaald: Size={sizeStr}, Color={color}, Stock={stockStr}");
                }
            }

            // Valideer dat er minimaal 1 variant is
            if (variants.Count == 0)
            {
                ModelState.AddModelError("", "Voeg minimaal één variant toe (maat, kleur, voorraad)");
                return View(shoe);
            }

            // Valideer dat alle varianten een SKU hebben VOORDAT we ze toevoegen
            foreach (var v in variants)
            {
                // Zorg ervoor dat SKU altijd een niet-lege waarde heeft (database NOT NULL constraint)
                if (string.IsNullOrWhiteSpace(v.SKU))
                {
                    v.SKU = GenerateSku(shoe, v.Size, v.Color);
                    Console.WriteLine($"⚠️ Variant zonder SKU gevonden, gegenereerd: {v.SKU}");
                }

                // Extra controle: als SKU nog steeds leeg is, gebruik een unieke waarde
                if (string.IsNullOrWhiteSpace(v.SKU))
                {
                    // Gebruik tijdelijke ID (0) voor fallback SKU
                    v.SKU = $"SKU-TEMP-{v.Size}-{DateTime.UtcNow.Ticks}";
                    Console.WriteLine($"⚠️ Variant SKU was nog steeds leeg, laatste fallback: {v.SKU}");
                }

                Console.WriteLine($"🔍 Voor SaveChanges: Variant SKU='{v.SKU}' (length={v.SKU.Length}, isNullOrWhiteSpace={string.IsNullOrWhiteSpace(v.SKU)})");
            }

            // Zet CreatedAt timestamp
            shoe.CreatedAt = DateTime.UtcNow;

            // Verwijder oude Size en Color velden (die zijn niet meer nodig)
            shoe.Size = null;
            shoe.Color = null;

            // Sla Shoe op om Shoe.Id te krijgen
            _context.Add(shoe);
            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ Shoe opgeslagen met ID: {shoe.Id}");

            // Update ShoeId voor alle varianten (nu hebben we shoe.Id)
            foreach (var v in variants)
            {
                v.ShoeId = shoe.Id;
                // Update SKU als deze een tijdelijke waarde heeft
                if (v.SKU.StartsWith("SKU-TEMP-"))
                {
                    v.SKU = GenerateSku(shoe, v.Size, v.Color);
                    Console.WriteLine($"🔄 SKU geüpdatet van temp naar definitief: {v.SKU}");
                }
            }

            // Voeg varianten toe
            _context.ShoeVariants.AddRange(variants);
            Console.WriteLine($"🔍 Aantal varianten om op te slaan: {variants.Count}");
            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ Schoen '{shoe.Name}' met {variants.Count} variant(en) succesvol toegevoegd!");
            TempData["SuccessBericht"] = $"✅ Schoen '{shoe.Name}' met {variants.Count} variant(en) succesvol toegevoegd!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Shoes/Edit/5
        [Authorize(Roles = "Admin")] // Alleen Admin mag schoenen bewerken
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shoe = await _context.Shoes
                .Include(s => s.Variants)
                .Include(s => s.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (shoe == null)
            {
                return NotFound();
            }
            return View(shoe);
        }

        // POST: Shoes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(long id, Shoe shoe)
        {
            if (id != shoe.Id)
            {
                return NotFound();
            }

            // Verwijder navigation property validatie errors
            ModelState.Remove("Category");
            ModelState.Remove("Variants");
            ModelState.Remove("Size");
            ModelState.Remove("Color");

            // Valideer handmatig de basisvelden
            if (string.IsNullOrWhiteSpace(shoe.Name) ||
                string.IsNullOrWhiteSpace(shoe.Brand) ||
                string.IsNullOrWhiteSpace(shoe.Description) ||
                shoe.CategoryId == 0 ||
                shoe.Price <= 0)
            {
                ModelState.AddModelError("", "Vul alle verplichte velden in");
                // Laad varianten opnieuw voor de view
                shoe = await _context.Shoes
                    .Include(s => s.Variants)
                    .Include(s => s.Category)
                    .FirstOrDefaultAsync(m => m.Id == id) ?? shoe;
                return View(shoe);
            }

            try
            {
                // Haal bestaande schoen op met varianten
                var bestaandeSchoen = await _context.Shoes
                    .Include(s => s.Variants)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (bestaandeSchoen == null)
                {
                    return NotFound();
                }

                // Update basisvelden
                bestaandeSchoen.Name = shoe.Name;
                bestaandeSchoen.Brand = shoe.Brand;
                bestaandeSchoen.Description = shoe.Description;
                bestaandeSchoen.CategoryId = shoe.CategoryId;
                bestaandeSchoen.Gender = shoe.Gender;
                bestaandeSchoen.Price = shoe.Price;
                bestaandeSchoen.ImageUrl = shoe.ImageUrl;
                bestaandeSchoen.IsActive = shoe.IsActive;

                // VERWERK VARIANTEN
                if (!Request.HasFormContentType)
                {
                    ModelState.AddModelError("", "Ongeldige form data");
                    bestaandeSchoen = await _context.Shoes
                        .Include(s => s.Variants)
                        .Include(s => s.Category)
                        .FirstOrDefaultAsync(m => m.Id == id) ?? bestaandeSchoen;
                    return View(bestaandeSchoen);
                }

                var form = Request.Form;
                var variantIndices = new List<int>();

                // Zoek alle variant indices uit form
                foreach (var key in form.Keys)
                {
                    if (key.StartsWith("Variants[") && key.Contains("].Size"))
                    {
                        var indexStr = key.Substring("Variants[".Length, key.IndexOf("]") - "Variants[".Length);
                        if (int.TryParse(indexStr, out int index))
                        {
                            variantIndices.Add(index);
                        }
                    }
                }

                if (variantIndices.Count == 0)
                {
                    ModelState.AddModelError("", "Voeg minimaal één variant toe (maat, kleur, voorraad)");
                    bestaandeSchoen = await _context.Shoes
                        .Include(s => s.Variants)
                        .Include(s => s.Category)
                        .FirstOrDefaultAsync(m => m.Id == id) ?? bestaandeSchoen;
                    return View(bestaandeSchoen);
                }

                // Verzamel nieuwe/gewijzigde varianten
                var nieuweVarianten = new List<ShoeVariant>();
                var bestaandeVariantIds = new HashSet<long>();

                foreach (var index in variantIndices)
                {
                    var variantIdStr = form[$"Variants[{index}].Id"].ToString() ?? "0";
                    var sizeStr = form[$"Variants[{index}].Size"].ToString() ?? "";
                    var color = form[$"Variants[{index}].Color"].ToString() ?? "";
                    var stockStr = form[$"Variants[{index}].Stock"].ToString() ?? "";

                    if (long.TryParse(variantIdStr, out long variantId) &&
                        int.TryParse(sizeStr, out int size) &&
                        int.TryParse(stockStr, out int stock) &&
                        !string.IsNullOrWhiteSpace(color))
                    {
                        if (variantId > 0)
                        {
                            // Bestaande variant bijwerken
                            var bestaandeVariant = bestaandeSchoen.Variants.FirstOrDefault(v => v.Id == variantId);
                            if (bestaandeVariant != null)
                            {
                                bestaandeVariant.Size = size;
                                bestaandeVariant.Color = color.Trim();
                                bestaandeVariant.Stock = stock;
                                // SKU wordt automatisch gegenereerd indien nodig (blijft hetzelfde)
                                if (string.IsNullOrWhiteSpace(bestaandeVariant.SKU))
                                {
                                    bestaandeVariant.SKU = GenerateSku(bestaandeSchoen, size, color);
                                }
                                bestaandeVariantIds.Add(variantId);
                            }
                        }
                        else
                        {
                            // Nieuwe variant toevoegen
                            var nieuweVariant = new ShoeVariant
                            {
                                ShoeId = id,
                                Size = size,
                                Color = color.Trim(),
                                Stock = stock,
                                SKU = GenerateSku(bestaandeSchoen, size, color)
                            };
                            nieuweVarianten.Add(nieuweVariant);
                        }
                    }
                }

                // Verwijder varianten die niet meer in de form zitten
                var variantenTeVerwijderen = bestaandeSchoen.Variants
                    .Where(v => !bestaandeVariantIds.Contains(v.Id))
                    .ToList();
                _context.ShoeVariants.RemoveRange(variantenTeVerwijderen);

                // Voeg nieuwe varianten toe
                if (nieuweVarianten.Any())
                {
                    _context.ShoeVariants.AddRange(nieuweVarianten);
                }

                // Sla alles op
                await _context.SaveChangesAsync();

                TempData["SuccessBericht"] = $"✅ Schoen '{bestaandeSchoen.Name}' met {variantIndices.Count} variant(en) succesvol bijgewerkt!";
                return RedirectToAction(nameof(Index));
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

        /// <summary>
        /// Genereert een SKU voor een schoen variant
        /// Format: BRAND-NAME-SIZE-COLOR (bijv. NIKE-AIRMAX-42-ZWART)
        /// </summary>
        private string GenerateSku(Shoe shoe, int size, string color, int? variantIndex = null)
        {
            // Genereer SKU: BRAND-NAME-SIZE-COLOR
            var brandClean = (shoe.Brand ?? "UNKNOWN").ToUpper().Replace(" ", "-").Replace("/", "-").Replace("\\", "-");
            var nameClean = (shoe.Name ?? "PRODUCT").ToUpper().Replace(" ", "-").Replace("/", "-").Replace("\\", "-");
            var colorClean = (color ?? "UNKNOWN").Trim().ToUpper().Replace(" ", "-").Replace("/", "-").Replace("\\", "-");
            var sku = $"{brandClean}-{nameClean}-{size}-{colorClean}";

            // Valideer dat SKU niet leeg is
            if (string.IsNullOrWhiteSpace(sku))
            {
                // Fallback: gebruik ShoeId, size en timestamp
                sku = $"SKU-{shoe.Id}-{size}-{DateTime.UtcNow.Ticks}";
            }

            return sku;
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
