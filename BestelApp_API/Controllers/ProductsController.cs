using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BestelApp_Models;

namespace BestelApp_API.Controllers
{
    /// <summary>
    /// Products API Controller
    /// Public endpoints voor product lijst
    /// Admin-only endpoints voor product beheer (CRUD)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(ApplicationDbContext context, ILogger<ProductsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ====================
        // PUBLIC ENDPOINTS
        // ====================

        /// <summary>
        /// GET api/products
        /// Haal alle actieve producten op
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllProducts()
        {
            try
            {
                var products = await _context.Shoes
                    .Where(s => s.IsActive)
                    .Include(s => s.Category)
                    .Include(s => s.Variants)
                    .Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Brand,
                        s.Description,
                        s.Price,
                        s.Gender,
                        s.ImageUrl,
                        Category = s.Category.Name,
                        CategoryId = s.Category.Id,
                        Variants = s.Variants.Select(v => new
                        {
                            v.Id,
                            v.Size,
                            v.Color,
                            v.Stock,
                            v.SKU,
                            v.IsAvailable
                        }).ToList(),
                        TotalStock = s.Variants.Sum(v => v.Stock)
                    })
                    .ToListAsync();

                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen producten");
                return StatusCode(500, "Er ging iets fout");
            }
        }

        /// <summary>
        /// GET api/products/{id}
        /// Haal 1 product op
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(long id)
        {
            try
            {
                var product = await _context.Shoes
                    .Where(s => s.Id == id && s.IsActive)
                    .Include(s => s.Category)
                    .Include(s => s.Variants)
                    .Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Brand,
                        s.Description,
                        s.Price,
                        s.Gender,
                        s.ImageUrl,
                        s.CreatedAt,
                        Category = new { s.Category.Id, s.Category.Name },
                        Variants = s.Variants.Select(v => new
                        {
                            v.Id,
                            v.Size,
                            v.Color,
                            v.Stock,
                            v.SKU,
                            v.IsAvailable
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (product == null)
                {
                    return NotFound($"Product met ID {id} niet gevonden");
                }

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen product {ProductId}", id);
                return StatusCode(500, "Er ging iets fout");
            }
        }

        /// <summary>
        /// GET api/products/categories
        /// Haal alle categorieën op
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Where(c => c.IsActive)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        c.Description,
                        ProductCount = c.Shoes.Count(s => s.IsActive)
                    })
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen categorieën");
                return StatusCode(500, "Er ging iets fout");
            }
        }

        // ====================
        // ADMIN-ONLY ENDPOINTS
        // ====================

        /// <summary>
        /// POST api/products
        /// Maak nieuw product (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateProduct([FromBody] Shoe shoe)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check of category bestaat
                var categoryExists = await _context.Categories.AnyAsync(c => c.Id == shoe.CategoryId);
                if (!categoryExists)
                {
                    return BadRequest($"Category met ID {shoe.CategoryId} bestaat niet");
                }

                shoe.CreatedAt = DateTime.UtcNow;
                shoe.IsActive = true;

                _context.Shoes.Add(shoe);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Product {ProductName} aangemaakt door admin", shoe.Name);

                return CreatedAtAction(nameof(GetProduct), new { id = shoe.Id }, shoe);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij aanmaken product");
                return StatusCode(500, "Er ging iets fout");
            }
        }

        /// <summary>
        /// PUT api/products/{id}
        /// Update product (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateProduct(long id, [FromBody] Shoe shoe)
        {
            try
            {
                if (id != shoe.Id)
                {
                    return BadRequest("Product ID komt niet overeen");
                }

                var existing = await _context.Shoes.FindAsync(id);
                if (existing == null)
                {
                    return NotFound($"Product met ID {id} niet gevonden");
                }

                // Update fields
                existing.Name = shoe.Name;
                existing.Brand = shoe.Brand;
                existing.Description = shoe.Description;
                existing.Price = shoe.Price;
                existing.CategoryId = shoe.CategoryId;
                existing.Gender = shoe.Gender;
                existing.ImageUrl = shoe.ImageUrl;
                existing.IsActive = shoe.IsActive;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Product {ProductId} bijgewerkt door admin", id);

                return Ok(existing);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij updaten product {ProductId}", id);
                return StatusCode(500, "Er ging iets fout");
            }
        }

        /// <summary>
        /// DELETE api/products/{id}
        /// Verwijder product (Admin only) - Soft delete
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProduct(long id)
        {
            try
            {
                var product = await _context.Shoes.FindAsync(id);
                if (product == null)
                {
                    return NotFound($"Product met ID {id} niet gevonden");
                }

                // Soft delete: zet IsActive op false
                product.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Product {ProductId} verwijderd (soft delete) door admin", id);

                return Ok(new { message = "Product verwijderd", productId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij verwijderen product {ProductId}", id);
                return StatusCode(500, "Er ging iets fout");
            }
        }

        /// <summary>
        /// POST api/products/{id}/variants
        /// Voeg variant toe aan product (Admin only)
        /// </summary>
        [HttpPost("{id}/variants")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddVariant(long id, [FromBody] ShoeVariant variant)
        {
            try
            {
                var product = await _context.Shoes.FindAsync(id);
                if (product == null)
                {
                    return NotFound($"Product met ID {id} niet gevonden");
                }

                variant.ShoeId = id;
                _context.ShoeVariants.Add(variant);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Variant toegevoegd aan product {ProductId}", id);

                return CreatedAtAction(nameof(GetProduct), new { id = id }, variant);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij toevoegen variant aan product {ProductId}", id);
                return StatusCode(500, "Er ging iets fout");
            }
        }

        /// <summary>
        /// PUT api/products/variants/{variantId}
        /// Update variant stock (Admin only)
        /// </summary>
        [HttpPut("variants/{variantId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateVariantStock(long variantId, [FromBody] UpdateStockRequest request)
        {
            try
            {
                var variant = await _context.ShoeVariants.FindAsync(variantId);
                if (variant == null)
                {
                    return NotFound($"Variant met ID {variantId} niet gevonden");
                }

                variant.Stock = request.Stock;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Voorraad variant {VariantId} bijgewerkt naar {Stock}", variantId, request.Stock);

                return Ok(variant);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij updaten variant stock");
                return StatusCode(500, "Er ging iets fout");
            }
        }
    }

    /// <summary>
    /// DTO voor stock update
    /// </summary>
    public class UpdateStockRequest
    {
        public int Stock { get; set; }
    }
}
