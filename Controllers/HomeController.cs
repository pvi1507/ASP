using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BC_ASP.Data;
using BC_ASP.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BC_ASP.Data;
using BC_ASP.Models;
using Microsoft.AspNetCore.Authorization;

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

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminDashboard()
        {
            var totalProducts = await _context.Products.CountAsync();
            var totalCategories = await _context.Categories.CountAsync();
            var totalOrders = await _context.Orders.CountAsync();

    var recentOrders = await _context.Orders
        .OrderByDescending(o => o.OrderDate)
        .Take(5)
        .Include(o => o.OrderDetails)
        .ThenInclude(od => od.Product)
        .ToListAsync();

    var recentProduct = await _context.Products
        .Where(p => p.IsActive && !string.IsNullOrEmpty(p.ImageUrl))
        .OrderByDescending(p => p.CreatedAt)
        .FirstOrDefaultAsync();

    ViewBag.Stats = new 
{
    TotalProducts = totalProducts,
    TotalCategories = totalCategories,
    TotalOrders = totalOrders,
    RecentOrders = recentOrders,
    RecentProductImage = recentProduct?.ImageUrl ?? "/images/products/product-6.jpg"
};


            return View();
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ContactMessages()
        {
            var messages = await _context.ContactMessages
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
            return View(messages);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ContactMessageDetails(int id)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }
            if (!message.IsRead)
            {
                message.IsRead = true;
                _context.Update(message);
                await _context.SaveChangesAsync();
            }
            return View(message);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
