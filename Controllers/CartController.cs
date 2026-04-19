using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using BC_ASP.Models;
using BC_ASP.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BC_ASP.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        private async Task<List<CartItem>> GetCart()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return new List<CartItem>();
            }

            return await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Add(int productId, int quantity = 1)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                TempData["Error"] = "Sản phẩm không tồn tại.";
                return RedirectToAction("Index", "Product");
            }

            ViewBag.Product = product;
            ViewBag.Quantity = quantity;
            ViewBag.Cart = await GetCart();
            ViewBag.Total = quantity * product.Price;

            return View("Checkout");
        }

[HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckoutSubmit(string customerName, string customerEmail, string customerPhone, string shippingAddress, string? note, int productId, int quantity)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Validate inputs
            if (productId <= 0 || quantity <= 0)
            {
                TempData["Error"] = $"Dữ liệu không hợp lệ: productId={productId}, quantity={quantity}";
                return RedirectToAction("Index", "Product");
            }

            var user = await _userManager.FindByIdAsync(userId);
            var product = await _context.Products.FindAsync(productId);

            if (product == null)
            {
                TempData["Error"] = $"Sản phẩm ID {productId} không tồn tại.";
                return RedirectToAction("Index", "Product");
            }

            if (product.Stock < quantity)
            {
                TempData["Error"] = $"Sản phẩm '{product.Name}' chỉ còn {product.Stock} chiếc trong kho.";
                ViewBag.Product = product;
                ViewBag.Quantity = quantity;
                ViewBag.Cart = await GetCart();
                ViewBag.Total = quantity * product.Price;
                return View("Checkout");
            }

            var totalAmount = quantity * product.Price;

            var order = new Order
            {
                UserId = userId,
                CustomerName = customerName,
                CustomerEmail = customerEmail,
                CustomerPhone = customerPhone,
                ShippingAddress = shippingAddress,
                Note = note,
                TotalAmount = totalAmount,
                OrderDate = DateTime.Now,
                Status = OrderStatus.Pending
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            var orderDetail = new OrderDetail
            {
                OrderId = order.Id,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = product.Price,
                Subtotal = totalAmount
            };
            _context.OrderDetails.Add(orderDetail);
            await _context.SaveChangesAsync();

            // Lưu vào giỏ hàng sau thanh toán thành công
            var existingCartItem = await _context.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);
            if (existingCartItem != null)
            {
                existingCartItem.Quantity += quantity;
            }
            else
            {
                _context.CartItems.Add(new CartItem
                {
                    UserId = userId,
                    ProductId = productId,
                    Quantity = quantity
                });
            }
            await _context.SaveChangesAsync();

            // Update stock
            product.Stock -= quantity;
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("CartCount", await _context.CartItems.CountAsync(c => c.UserId == userId));

            // Debug info
            TempData["Debug"] = $"UserId: {userId}, Added ProductId: {productId}, Qty: {quantity}, Cart count: {await _context.CartItems.CountAsync(c => c.UserId == userId)}";

            TempData["Success"] = "Đặt hàng thành công! Sản phẩm đã lưu vào giỏ hàng. Kiểm tra giỏ hàng.";
            return RedirectToAction("Index");  // Redirect to Cart/Index to verify immediately
        }

        // GET: /Cart - View saved products
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var cart = await GetCart();
            HttpContext.Session.SetInt32("CartCount", cart.Count);
            ViewBag.Count = cart.Count;
            ViewBag.Title = "Sản phẩm đã lưu";
            ViewBag.DebugUserId = userId;
            ViewBag.DebugCartCount = cart.Count;
            ViewBag.DebugCartItems = string.Join(", ", cart.Select(c => $"P{c.ProductId}:{c.Quantity}"));
            return View(cart);
        }
    }
}
