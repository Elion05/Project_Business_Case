using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BestelApp_Web.Controllers
{
    /// <summary>
    /// Admin dashboard (beheerdersportaal)
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

