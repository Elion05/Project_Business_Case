using BestelApp_Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BestelApp_Web.Controllers
{
    /// <summary>
    /// Admin voorraad beheer (ShoeVariants: maat/kleur/stock)
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AdminStockController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminStockController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminStock
        public async Task<IActionResult> Index(string? zoek)
        {
            var query = _context.Shoes
                .Include(s => s.Category)
                .Include(s => s.Variants)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(zoek))
            {
                query = query.Where(s =>
                    s.Name.Contains(zoek) ||
                    s.Brand.Contains(zoek));
            }

            ViewBag.Zoek = zoek ?? string.Empty;
            var schoenen = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
            return View(schoenen);
        }

        // GET: AdminStock/Edit/5
        public async Task<IActionResult> Edit(long id)
        {
            var shoe = await _context.Shoes
                .Include(s => s.Category)
                .Include(s => s.Variants)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shoe == null)
            {
                return NotFound();
            }

            var model = new VoorraadBeheerViewModel
            {
                SchoenId = shoe.Id,
                Naam = shoe.Name,
                Merk = shoe.Brand,
                CategoryNaam = shoe.Category?.Name ?? "Schoenen",
                AfbeeldingUrl = shoe.ImageUrl,
                Varianten = shoe.Variants
                    .OrderBy(v => v.Size)
                    .ThenBy(v => v.Color)
                    .Select(v => new VariantRijViewModel
                    {
                        VariantId = v.Id,
                        Maat = v.Size,
                        Kleur = v.Color,
                        Stock = v.Stock
                    }).ToList()
            };

            return View(model);
        }

        // POST: AdminStock/UpdateVariant
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateVariant(UpdateVariantViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["FoutBericht"] = "Ongeldige invoer voor voorraad.";
                return RedirectToAction(nameof(Edit), new { id = model.SchoenId });
            }

            var variant = await _context.ShoeVariants.FirstOrDefaultAsync(v => v.Id == model.VariantId);
            if (variant == null)
            {
                TempData["FoutBericht"] = "Variant niet gevonden.";
                return RedirectToAction(nameof(Edit), new { id = model.SchoenId });
            }

            variant.Stock = model.NieuweStock;
            await _context.SaveChangesAsync();

            TempData["SuccessBericht"] = "Voorraad bijgewerkt.";
            return RedirectToAction(nameof(Edit), new { id = model.SchoenId });
        }

        // POST: AdminStock/AddVariant
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVariant(AddVariantViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["FoutBericht"] = "Ongeldige invoer voor nieuwe variant.";
                return RedirectToAction(nameof(Edit), new { id = model.SchoenId });
            }

            var shoe = await _context.Shoes.FirstOrDefaultAsync(s => s.Id == model.SchoenId);
            if (shoe == null)
            {
                TempData["FoutBericht"] = "Schoen niet gevonden.";
                return RedirectToAction(nameof(Index));
            }

            var nieuweVariant = new ShoeVariant
            {
                ShoeId = shoe.Id,
                Size = model.Maat,
                Color = model.Kleur,
                Stock = model.Stock
            };

            _context.ShoeVariants.Add(nieuweVariant);
            await _context.SaveChangesAsync();

            TempData["SuccessBericht"] = "Nieuwe variant toegevoegd.";
            return RedirectToAction(nameof(Edit), new { id = model.SchoenId });
        }

        // POST: AdminStock/DeleteVariant
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVariant(long schoenId, long variantId)
        {
            var variant = await _context.ShoeVariants.FirstOrDefaultAsync(v => v.Id == variantId && v.ShoeId == schoenId);
            if (variant == null)
            {
                TempData["FoutBericht"] = "Variant niet gevonden.";
                return RedirectToAction(nameof(Edit), new { id = schoenId });
            }

            _context.ShoeVariants.Remove(variant);
            await _context.SaveChangesAsync();

            TempData["SuccessBericht"] = "Variant verwijderd.";
            return RedirectToAction(nameof(Edit), new { id = schoenId });
        }
    }

    // =========================
    // ViewModels (basic)
    // =========================
    public class VoorraadBeheerViewModel
    {
        public long SchoenId { get; set; }
        public string Naam { get; set; } = string.Empty;
        public string Merk { get; set; } = string.Empty;
        public string CategoryNaam { get; set; } = string.Empty;
        public string AfbeeldingUrl { get; set; } = string.Empty;
        public List<VariantRijViewModel> Varianten { get; set; } = new();
    }

    public class VariantRijViewModel
    {
        public long VariantId { get; set; }
        public int Maat { get; set; }
        public string Kleur { get; set; } = string.Empty;
        public int Stock { get; set; }
    }

    public class UpdateVariantViewModel
    {
        [Required]
        public long SchoenId { get; set; }

        [Required]
        public long VariantId { get; set; }

        [Range(0, 10000)]
        public int NieuweStock { get; set; }
    }

    public class AddVariantViewModel
    {
        [Required]
        public long SchoenId { get; set; }

        [Range(20, 50)]
        public int Maat { get; set; }

        [Required]
        [MaxLength(50)]
        public string Kleur { get; set; } = string.Empty;

        [Range(0, 10000)]
        public int Stock { get; set; }
    }
}

