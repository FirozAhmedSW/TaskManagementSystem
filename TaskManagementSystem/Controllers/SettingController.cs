using Microsoft.AspNetCore.Mvc;

namespace TaskManagementSystem.Controllers
{
    public class SettingController : Controller
    {
        // GET: Settings
        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }
    }
}
