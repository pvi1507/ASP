using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BC_ASP.Data;
using BC_ASP.Models;

namespace BC_ASP.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var featuredProducts = await _context.Products
                .Where(p => p.IsFeatured && p.IsActive)
                .Include(p => p.Category)
                .Take(8)
                .ToListAsync();

            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .Take(6)
                .ToListAsync();

            ViewBag.FeaturedProducts = featuredProducts;
            ViewBag.Categories = categories;

            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
