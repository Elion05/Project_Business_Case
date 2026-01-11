using BestelApp_Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace BestelApp_Web.Controllers
{
    
    public class UsersController : Controller
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
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
                                          where ur.UserId == user.Id
                                          select ur.RoleId).ToList()
                             };

            ViewData["username"] = username;
            var roles = await _context.Roles.ToListAsync();
            roles.Add(new IdentityRole { Id = "?", Name = "?" });
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
            ViewData["AllRoles"] = new MultiSelectList(_context.Roles.OrderBy(r => r.Name), "Id", "Id", roleViewModel.Roles);
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
}
