using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;   // Change namespace if needed
using Microsoft.AspNetCore.Http;
using TaskManagementSystem.DataContext;

namespace TaskManagementSystem.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userName = HttpContext.Session.GetString("UserName");

            if (string.IsNullOrEmpty(userName))
                return RedirectToAction("Login", "Account");

            var user = _context.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserName == userName && !u.IsDeleted);

            if (user == null)
                return RedirectToAction("Login", "Account");

            return View(user);
        }
    }
}
