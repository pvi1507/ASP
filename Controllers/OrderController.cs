using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BC_ASP.Data;
using BC_ASP.Models;

namespace BC_ASP.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public OrderController(
            ApplicationDbContext context, 
            Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Order - Customer sees their own orders, Admin/Employee sees all
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var isAdmin = User.IsInRole("Admin");
            var isEmployee = User.IsInRole("Employee");

            IQueryable<Order> ordersQuery;

            if (isAdmin || isEmployee)
            {
                // Admin/Employee sees all orders
                ordersQuery = _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .OrderByDescending(o => o.OrderDate);
            }
            else
            {
                // Customer sees only their own orders
                ordersQuery = _context.Orders
                    .Where(o => o.UserId == user.Id)
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .OrderByDescending(o => o.OrderDate);
            }

            return View(await ordersQuery.ToListAsync());
        }

        // GET: /Order/Details/5
        [Authorize]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            // Check if user is authorized to view this order
            var isAdmin = User.IsInRole("Admin");
            var isEmployee = User.IsInRole("Employee");
            
            if (!isAdmin && !isEmployee && order.UserId != user.Id)
            {
                return Forbid();
            }

            return View(order);
        }

        // GET: /Order/Edit/5 - Admin/Employee only
        [Authorize(Roles = "Admin,Employee")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            ViewBag.StatusList = new List<SelectListItem>
            {
                new SelectListItem { Value = "0", Text = "Chờ xác nhận", Selected = order.Status == OrderStatus.Pending },
                new SelectListItem { Value = "1", Text = "Đã xác nhận", Selected = order.Status == OrderStatus.Confirmed },
                new SelectListItem { Value = "2", Text = "Đang giao hàng", Selected = order.Status == OrderStatus.Shipping },
                new SelectListItem { Value = "3", Text = "Hoàn thành", Selected = order.Status == OrderStatus.Completed },
                new SelectListItem { Value = "4", Text = "Đã hủy", Selected = order.Status == OrderStatus.Cancelled }
            };

            return View(order);
        }

        // POST: /Order/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Employee")]
        public async Task<IActionResult> Edit(int id, Order order)
        {
            if (id != order.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(order);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật đơn hàng thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrderExists(order.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            
            ViewBag.StatusList = new List<SelectListItem>
            {
                new SelectListItem { Value = "0", Text = "Chờ xác nhận", Selected = order.Status == OrderStatus.Pending },
                new SelectListItem { Value = "1", Text = "Đã xác nhận", Selected = order.Status == OrderStatus.Confirmed },
                new SelectListItem { Value = "2", Text = "Đang giao hàng", Selected = order.Status == OrderStatus.Shipping },
                new SelectListItem { Value = "3", Text = "Hoàn thành", Selected = order.Status == OrderStatus.Completed },
                new SelectListItem { Value = "4", Text = "Đã hủy", Selected = order.Status == OrderStatus.Cancelled }
            };
            
            return View(order);
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }
    }
}
