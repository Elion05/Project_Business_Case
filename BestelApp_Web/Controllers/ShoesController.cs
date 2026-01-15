using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BestelApp_Models;

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
        public IActionResult Create()
        {
            return View();
        }

        // POST: Shoes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Brand,Price,Size,Color")] Shoe shoe)
        {
            if (ModelState.IsValid)
            {
                _context.Add(shoe);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(shoe);
        }

        // GET: Shoes/Edit/5
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
        public async Task<IActionResult> Edit(long id, [Bind("Id,Name,Brand,Price,Size,Color")] Shoe shoe)
        {
            if (id != shoe.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(shoe);
                    await _context.SaveChangesAsync();
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

            // Verstuur naar Backend API (niet meer direct naar RabbitMQ!)
            var success = await _orderApiService.PlaceOrderAsync(shoe);

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
