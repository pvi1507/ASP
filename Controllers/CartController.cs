using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BC_ASP.Models;
using BC_ASP.Data;
using BC_ASP.Extensions;
using Microsoft.EntityFrameworkCore;

namespace BC_ASP.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Cart
        public IActionResult Index()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
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
        public IActionResult Add(int productId, int quantity = 1)
        {
            var product = _context.Products.Find(productId);
            if (product == null)
            {
                return NotFound();
            }

            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            
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
            TempData["Success"] = "Đã thêm vào giỏ hàng!";
            
            return RedirectToAction("Index");
        }

        // POST: /Cart/Remove/5
        [HttpPost]
        [Authorize]
        public IActionResult Remove(int productId)
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            var item = cart.FirstOrDefault(x => x.ProductId == productId);
            
            if (item != null)
            {
                cart.Remove(item);
                HttpContext.Session.Set("Cart", cart);
                TempData["Success"] = "Đã xóa khỏi giỏ hàng!";
            }
            
            return RedirectToAction("Index");
        }

        // POST: /Cart/Update
        [HttpPost]
        [Authorize]
        public IActionResult Update(Dictionary<int, int> quantities)
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            
            var itemsToRemove = new List<CartItem>();
            foreach (var item in cart)
            {
                if (quantities.ContainsKey(item.ProductId))
                {
                    item.Quantity = quantities[item.ProductId];
                    if (item.Quantity <= 0)
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
            TempData["Success"] = "Đã cập nhật giỏ hàng!";
            
            return RedirectToAction("Index");
        }

        // GET: /Cart/Checkout
        [Authorize]  // Require login to checkout
        public IActionResult Checkout()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            
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

                // Clear cart
                HttpContext.Session.Remove("Cart");
                
                TempData["Success"] = "Đặt hàng thành công!";
                return RedirectToAction("Details", "Order", new { id = order.Id });
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
            return View(model);
        }

        // POST: /Cart/Clear
        [HttpPost]
        [Authorize]
        public IActionResult Clear()
        {
            HttpContext.Session.Remove("Cart");
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
