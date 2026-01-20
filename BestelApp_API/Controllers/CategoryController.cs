using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BestelApp_Models;

namespace BestelApp_API.Controllers
{
    /// <summary>
    /// Category Controller voor categorie beheer
    /// Public endpoints voor lijst, Admin-only voor CRUD
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CategoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(ApplicationDbContext context, ILogger<CategoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ====================
        // PUBLIC ENDPOINTS
        // ====================

        /// <summary>
        /// GET api/category
        /// Haal alle actieve categorieën op
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllCategories()
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
                        ProductCount = c.Shoes.Count(s => s.IsActive),
                        TotalStock = c.Shoes.Where(s => s.IsActive).SelectMany(s => s.Variants).Sum(v => v.Stock)
                    })
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen categorieën");
                return StatusCode(500, new { message = "Er ging iets fout bij het ophalen van categorieën" });
            }
        }

        /// <summary>
        /// GET api/category/{id}
        /// Haal 1 categorie op met producten
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(long id)
        {
            try
            {
                var category = await _context.Categories
                    .Where(c => c.Id == id && c.IsActive)
                    .Include(c => c.Shoes.Where(s => s.IsActive))
                        .ThenInclude(s => s.Variants)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        c.Description,
                        Products = c.Shoes.Select(s => new
                        {
                            s.Id,
                            s.Name,
                            s.Brand,
                            s.Price,
                            s.Gender,
                            s.ImageUrl,
                            TotalStock = s.Variants.Sum(v => v.Stock),
                            VariantCount = s.Variants.Count
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (category == null)
                {
                    return NotFound(new { message = $"Categorie met ID {id} niet gevonden" });
                }

                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij ophalen categorie {CategoryId}", id);
                return StatusCode(500, new { message = "Er ging iets fout" });
            }
        }

        // ====================
        // ADMIN-ONLY ENDPOINTS
        // ====================

        /// <summary>
        /// POST api/category
        /// Maak nieuwe categorie (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryCreateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check of naam al bestaat
                var exists = await _context.Categories.AnyAsync(c => c.Name == request.Name);
                if (exists)
                {
                    return BadRequest(new { message = $"Categorie met naam '{request.Name}' bestaat al" });
                }

                var category = new Category
                {
                    Name = request.Name,
                    Description = request.Description ?? string.Empty,
                    IsActive = true
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Categorie {CategoryName} aangemaakt door admin", category.Name);

                return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij aanmaken categorie");
                return StatusCode(500, new { message = "Er ging iets fout" });
            }
        }

        /// <summary>
        /// PUT api/category/{id}
        /// Update categorie (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCategory(long id, [FromBody] CategoryUpdateRequest request)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return NotFound(new { message = $"Categorie met ID {id} niet gevonden" });
                }

                // Check of nieuwe naam al bestaat (bij andere categorie)
                if (!string.IsNullOrEmpty(request.Name) && request.Name != category.Name)
                {
                    var nameExists = await _context.Categories.AnyAsync(c => c.Name == request.Name && c.Id != id);
                    if (nameExists)
                    {
                        return BadRequest(new { message = $"Categorie met naam '{request.Name}' bestaat al" });
                    }
                    category.Name = request.Name;
                }

                if (request.Description != null)
                {
                    category.Description = request.Description;
                }

                if (request.IsActive.HasValue)
                {
                    category.IsActive = request.IsActive.Value;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Categorie {CategoryId} bijgewerkt door admin", id);

                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij updaten categorie {CategoryId}", id);
                return StatusCode(500, new { message = "Er ging iets fout" });
            }
        }

        /// <summary>
        /// DELETE api/category/{id}
        /// Verwijder categorie (Admin only) - Soft delete
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCategory(long id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Shoes)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    return NotFound(new { message = $"Categorie met ID {id} niet gevonden" });
                }

                // Check of er producten aan gekoppeld zijn
                if (category.Shoes.Any(s => s.IsActive))
                {
                    return BadRequest(new
                    {
                        message = $"Kan categorie niet verwijderen: er zijn {category.Shoes.Count(s => s.IsActive)} actieve producten gekoppeld",
                        productCount = category.Shoes.Count(s => s.IsActive)
                    });
                }

                // Soft delete
                category.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Categorie {CategoryId} verwijderd (soft delete) door admin", id);

                return Ok(new { message = "Categorie verwijderd", categoryId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fout bij verwijderen categorie {CategoryId}", id);
                return StatusCode(500, new { message = "Er ging iets fout" });
            }
        }
    }

    // Request DTOs
    public class CategoryCreateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class CategoryUpdateRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }
}
