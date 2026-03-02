using Microsoft.AspNetCore.Mvc;

namespace CryptoPriceTracker.Api.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}