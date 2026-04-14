using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using BC_ASP.Models;
using BC_ASP.Data;
using BC_ASP.Extensions;
using Microsoft.EntityFrameworkCore;

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
            return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }

        public async Task LoadDbCartToSession()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return;

            var dbCart = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            HttpContext.Session.Set("Cart", dbCart);
        }

        private async Task SyncSessionToDb()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return;

            var sessionCart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            
            // Clear existing DB cart for user
            var existingDbCart = await _context.CartItems.Where(c => c.UserId == userId).ToListAsync();
            _context.CartItems.RemoveRange(existingDbCart);

            // Add session cart to DB (without Product navigation, as it's EF tracked)
            foreach (var item in sessionCart)
            {
                var dbItem = new CartItem
                {
                    UserId = userId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                };
                _context.CartItems.Add(dbItem);
            }

            await _context.SaveChangesAsync();
        }

        private async Task<List<CartItem>> GetCart()
        {
            var userId = GetCurrentUserId();
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();

            if (!string.IsNullOrEmpty(userId))
            {
                // Load/merge from DB
                await LoadDbCartToSession();
                cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
                await SyncSessionToDb(); // Keep DB in sync
            }

            // Load Products if missing
            foreach (var item in cart)
            {
                if (item.Product == null)
                {
                    item.Product = await _context.Products.FindAsync(item.ProductId);
                }
            }

            return cart;
        }

        private async Task ClearUserCart()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) 
            {
                HttpContext.Session.Remove("Cart");
                return;
            }

            var dbCart = await _context.CartItems.Where(c => c.UserId == userId).ToListAsync();
            _context.CartItems.RemoveRange(dbCart);
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove("Cart");
        }

        // GET: /Cart
        public async Task<IActionResult> Index()
        {
            var cart = await GetCart();
            decimal total = 0;
            foreach (var item in cart)
            {
                if (item.Product != null)
                {
                    total += item.Quantity * item.Product.Price;
                }
            }
            ViewBag.Total = total;
            return View(cart);
        }

        // POST: /Cart/Add/5
        [HttpPost]
        [Authorize]  // Require login to add to cart
        public async Task<IActionResult> Add(int productId, int quantity = 1)
        {
            var product = _context.Products.Find(productId);
            if (product == null)
            {
                return NotFound();
            }

            var cart = await GetCart();
            
            var existingItem = cart.FirstOrDefault(x => x.ProductId == productId);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Add(new CartItem
                {
                    ProductId = productId,
                    Product = product,
                    Quantity = quantity
                });
            }

            HttpContext.Session.Set("Cart", cart);
            await SyncSessionToDb();
            TempData["Success"] = "Đã thêm vào giỏ hàng!";
            
            return RedirectToAction("Index");
        }

        // POST: /Cart/Remove/5
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Remove(int productId)
        {
            var cart = await GetCart();
            var item = cart.FirstOrDefault(x => x.ProductId == productId);
            
            if (item != null)
            {
                cart.Remove(item);
                HttpContext.Session.Set("Cart", cart);
                await SyncSessionToDb();
                TempData["Success"] = "Đã xóa khỏi giỏ hàng!";
            }
            
            return RedirectToAction("Index");
        }

        // POST: /Cart/Update
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Update([FromForm]Dictionary<int, int> quantities)
        {
            var cart = await GetCart();
            
            var itemsToRemove = new List<CartItem>();
            foreach (var kvp in quantities)
            {
                var productId = kvp.Key;
                var quantity = kvp.Value;
                
                var item = cart.FirstOrDefault(x => x.ProductId == productId);
                if (item != null)
                {
                    item.Quantity = quantity;
                    if (quantity <= 0)
                    {
                        itemsToRemove.Add(item);
                    }
                }
            }

            foreach (var item in itemsToRemove)
            {
                cart.Remove(item);
            }
            
            HttpContext.Session.Set("Cart", cart);
            await SyncSessionToDb();
            TempData["Success"] = "Đã cập nhật giỏ hàng!";
            
            return RedirectToAction("Index");
        }

        // GET: /Cart/Checkout
        [Authorize]  // Require login to checkout
        public async Task<IActionResult> Checkout()
        {
            var cart = await GetCart();
            
            if (!cart.Any())
            {
                TempData["Error"] = "Giỏ hàng trống!";
                return RedirectToAction("Index");
            }
            
            decimal total = 0;
            foreach (var item in cart)
            {
                if (item.Product != null)
                {
                    total += item.Quantity * item.Product.Price;
                }
            }

            ViewBag.Total = total;
            return View(cart);
        }

        // POST: /Cart/Checkout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            
            if (!cart.Any())
            {
                TempData["Error"] = "Giỏ hàng trống!";
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
                if (user == null)
                {
                    return NotFound();
                }

                decimal totalAmount = 0;
                foreach (var item in cart)
                {
                    if (item.Product != null)
                    {
                        totalAmount += item.Quantity * item.Product.Price;
                    }
                }

                var order = new Order
                {
                    UserId = user.Id,
                    CustomerName = model.CustomerName,
                    CustomerEmail = model.CustomerEmail,
                    CustomerPhone = model.CustomerPhone,
                    ShippingAddress = model.ShippingAddress,
                    Note = model.Note,
                    TotalAmount = totalAmount,
                    Status = OrderStatus.Pending
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in cart)
                {
                    if (item.Product != null)
                    {
                        var orderDetail = new OrderDetail
                        {
                            OrderId = order.Id,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = item.Product.Price,
                            Subtotal = item.Quantity * item.Product.Price
                        };
                        _context.OrderDetails.Add(orderDetail);
                    }
                }

                await _context.SaveChangesAsync();

                // Keep cart for re-ordering - removed ClearUserCart()
                
                TempData["Success"] = "Đặt hàng thành công!";
                return RedirectToAction("Details", "Order", new { id = order.Id });
            }

            var refreshedCart = await GetCart();
            decimal total = 0;
            foreach (var item in refreshedCart)
            {
                if (item.Product != null)
                {
                    total += item.Quantity * item.Product.Price;
                }
            }
            ViewBag.Total = total;
            return View(model);
        }

        // POST: /Cart/Clear
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Clear()
        {
            await ClearUserCart();
            TempData["Success"] = "Đã xóa giỏ hàng!";
            return RedirectToAction("Index");
        }
    }

    public class CheckoutViewModel
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string? Note { get; set; }
    }
}
