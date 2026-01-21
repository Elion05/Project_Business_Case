using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using BestelApp_Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BestelApp_Web.Controllers
{
    [Authorize(Roles = "Admin")] // Alleen Admin mag gebruikers beheren
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Users
        public async Task<ActionResult> Index(string username = "", string roleId = "?")
        {
            var usersQuery = from Users user in _context.Users
                             where (user.UserName != "dummy"
                             && (username == "" || (user.UserName != null && user.UserName.Contains(username))))
                             && (roleId == "?" || (from ur in _context.UserRoles
                                                   where ur.UserId == user.Id
                                                   select ur.RoleId).Contains(roleId))
                             orderby user.UserName
                             select new UserViewModel
                             {
                                 Id = user.Id,
                                 UserName = user.UserName ?? string.Empty,
                                 Email = user.Email ?? string.Empty,
                                 Blocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                                 Roles = (from ur in _context.UserRoles
                                          join r in _context.Roles on ur.RoleId equals r.Id
                                          where ur.UserId == user.Id
                                          select r.Name ?? string.Empty).ToList()
                             };

            ViewData["username"] = username;
            var roles = await _context.Roles.ToListAsync();
            roles.Add(new IdentityRole { Id = "?", Name = "Alle rollen" });
            ViewData["Roles"] = new SelectList(roles, "Id", "Name", roles.First(r => r.Id == roleId));
            return View(await usersQuery.ToListAsync());
        }

        public async Task<IActionResult> BlockUnblock(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                // Unblock user
                user.LockoutEnd = null;
            }
            else
            {
                // Block user
                user.LockoutEnd = DateTimeOffset.MaxValue;
            }
            _context.Update(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Edit/{id}
        public async Task<IActionResult> Edit(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            var model = new AdminGebruikerEditViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Address = user.Address ?? string.Empty,
                City = user.City ?? string.Empty,
                PostalCode = user.PostalCode ?? string.Empty,
                Country = user.Country ?? string.Empty
            };

            return View(model);
        }

        // POST: Users/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminGebruikerEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["FoutBericht"] = "Ongeldige invoer.";
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == model.Id);
            if (user == null)
            {
                return NotFound();
            }

            // Basic profielvelden aanpassen (we wijzigen UserName niet om problemen te vermijden)
            user.Email = model.Email;
            user.NormalizedEmail = (model.Email ?? string.Empty).ToUpperInvariant();

            user.FirstName = model.FirstName ?? string.Empty;
            user.LastName = model.LastName ?? string.Empty;
            user.PhoneNumber = model.PhoneNumber ?? string.Empty;

            user.Address = model.Address ?? string.Empty;
            user.City = model.City ?? string.Empty;
            user.PostalCode = model.PostalCode ?? string.Empty;
            user.Country = model.Country ?? string.Empty;

            _context.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessBericht"] = "Gebruiker bijgewerkt.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Roles(string? Id)
        {
            if (Id == null)
                return RedirectToAction(nameof(Index));
            // Find the user by their ID.
            Users? user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Id);
            if (user == null)
            {
                return NotFound();
            }
            // Create a view model to hold the user's name and their current roles.
            UserRolesViewModel roleViewModel = new UserRolesViewModel
            {
                UserName = user.UserName ?? string.Empty,
                // Get a list of role IDs for the user.
                Roles = await (from userRole in _context.UserRoles
                               where userRole.UserId == user.Id
                               orderby userRole.RoleId
                               select userRole.RoleId).ToListAsync()
            };
            // Create a MultiSelectList containing all available roles, with the user's current roles pre-selected.
            ViewData["AllRoles"] = new MultiSelectList(_context.Roles.OrderBy(r => r.Name), "Id", "Name", roleViewModel.Roles);
            // Return the view, passing the view model to it.
            return View(roleViewModel);
        }

        // HTTP POST action to handle the submission of the roles management form.
        [HttpPost]
        public IActionResult Roles([Bind("UserName, Roles")] UserRolesViewModel _model)
        {
            // Find the user based on the username from the submitted model.
            Users? user = _context.Users.FirstOrDefault(u => u.UserName == _model.UserName);

            if (user == null)
            {
                return NotFound();
            }

            // Get all existing roles for this user.
            List<IdentityUserRole<string>> roles = _context.UserRoles.Where(ur => ur.UserId == user.Id).ToList();
            // Remove all current roles from the user.
            foreach (IdentityUserRole<string> role in roles)
                _context.Remove(role);

            // Assign the new set of roles to the user.         
            if (_model.Roles != null)
                foreach (string roleId in _model.Roles)
                    _context.UserRoles.Add(new IdentityUserRole<string> { RoleId = roleId, UserId = user.Id });

            _context.SaveChanges();

            return RedirectToAction("Index");
        }
    }

    public class UserViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Display(Name = "User")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Block or unblock")]
        public bool Blocked { get; set; }

        [Display(Name = "Roles")]
        public List<string> Roles { get; set; } = new();
    }

    public class UserRolesViewModel
    {
        [Display(Name = "User")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "Roles")]
        public List<string> Roles { get; set; } = new();
    }

    public class AdminGebruikerEditViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Display(Name = "Gebruikersnaam")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Voornaam")]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Achternaam")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Telefoon")]
        public string PhoneNumber { get; set; } = string.Empty;

        [MaxLength(200)]
        [Display(Name = "Adres")]
        public string Address { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Stad")]
        public string City { get; set; } = string.Empty;

        [MaxLength(20)]
        [Display(Name = "Postcode")]
        public string PostalCode { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Land")]
        public string Country { get; set; } = string.Empty;
    }
}
