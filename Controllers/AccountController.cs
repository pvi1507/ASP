using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BC_ASP.Data;
using BC_ASP.Models;
using BC_ASP.Services;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BC_ASP.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailService = emailService;
        }

        // GET: /Account/Login
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(
                    model.Email, 
                    model.Password, 
                    model.RememberMe, 
                    lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Đăng nhập thất bại. Vui lòng kiểm tra lại email và mật khẩu.");
                }
            }
            return View(model);
        }

        // GET: /Account/Register
        [AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Register - Step 1: Send OTP
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            
            if (ModelState.IsValid)
            {
                // Check if email already exists
                // TEMP DISABLE EMAIL CHECK FOR TESTING
                /*
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError(string.Empty, "Email này đã được sử dụng.");
                    return View(model);
                }
                */

                // Generate OTP
                var otp = GenerateOTP();
                
                // Store user info in session
                HttpContext.Session.SetString("Reg_Email", model.Email);
                HttpContext.Session.SetString("Reg_Password", model.Password);
                HttpContext.Session.SetString("Reg_FullName", model.FullName);
                HttpContext.Session.SetString("Reg_Address", model.Address ?? "");
                HttpContext.Session.SetString("Reg_OTP", otp);
                HttpContext.Session.SetString("Reg_OTP_Time", DateTime.Now.AddMinutes(5).ToString());
                
                TempData["Success"] = $"Mã OTP đã được gửi đến {model.Email}!";
                return RedirectToAction("VerifyOTP");
            }
            return View(model);
        }

        // GET: /Account/VerifyOTP
        [AllowAnonymous]
        public IActionResult VerifyOTP()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Reg_OTP")))
            {
                TempData["Error"] = "Phiên đăng ký đã hết hạn. Vui lòng đăng ký lại.";
                return RedirectToAction("Register");
            }
            
            // Clear any existing model errors
            ModelState.Clear();
            
            return View();
        }

        // POST: /Account/VerifyOTP
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOTP(string otp, string? returnUrl = null)
        {
            var storedOTP = HttpContext.Session.GetString("Reg_OTP");
            var otpExpiry = HttpContext.Session.GetString("Reg_OTP_Time"); // Remove conflicting email var

            var sessionEmail = HttpContext.Session.GetString("Reg_Email");
            if (string.IsNullOrEmpty(storedOTP) || string.IsNullOrEmpty(sessionEmail))
            {
                TempData["Error"] = "Phiên đăng ký đã hết hạn. Vui lòng đăng ký lại.";
                return RedirectToAction("Register");
            }

            // Check OTP expiry
            if (!string.IsNullOrEmpty(otpExpiry))
            {
                var expiryTime = DateTime.Parse(otpExpiry);
                if (DateTime.Now > expiryTime)
                {
                    TempData["Error"] = "Mã OTP đã hết hạn. Vui lòng đăng ký lại.";
                    HttpContext.Session.Remove("Reg_OTP");
                    return RedirectToAction("Register");
                }
            }

            // Validate OTP input
            if (string.IsNullOrEmpty(otp))
            {
                TempData["Error"] = "Vui lòng nhập mã OTP.";
                return View();
            }

            if (otp.Length != 6)
            {
                TempData["Error"] = "Mã OTP phải gồm 6 chữ số.";
                return View();
            }

            if (otp == storedOTP)
            {
                try
                {
                // Get session data
                    var regEmail = HttpContext.Session.GetString("Reg_Email") ?? "";
                    var password = HttpContext.Session.GetString("Reg_Password") ?? "";
                    var fullName = HttpContext.Session.GetString("Reg_FullName") ?? "";
                    var address = HttpContext.Session.GetString("Reg_Address") ?? "";

                    // Create user
                    var user = new ApplicationUser
                    {
                        UserName = fullName,
                        Email = sessionEmail,
                        FullName = fullName,
                        Address = address,
                        EmailConfirmed = true
                    };

                    var result = await _userManager.CreateAsync(user, password);
                    
                    if (result.Succeeded)
                    {
                        // Add Customer role
                        await _userManager.AddToRoleAsync(user, "Customer");
                        
                        // Clear session
                        HttpContext.Session.Remove("Reg_OTP");
                        HttpContext.Session.Remove("Reg_Email");
                        HttpContext.Session.Remove("Reg_Password");
                        HttpContext.Session.Remove("Reg_FullName");
                        HttpContext.Session.Remove("Reg_Address");
                        HttpContext.Session.Remove("Reg_OTP_Time");
                        
                        // Sign in
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        
                        TempData["Success"] = "Đăng ký thành công! Chào mừng bạn!";
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        // Show exact errors
                        TempData["Error"] = "Lỗi tạo user: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Lỗi hệ thống: " + ex.Message;
                }
            }
            else
            {
                TempData["Error"] = "Mã OTP không chính xác. Vui lòng thử lại.";
            }

            return View();
        }

        // POST: /Account/ResendOTP
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResendOTP()
        {
            var email = HttpContext.Session.GetString("Reg_Email");
            
            if (string.IsNullOrEmpty(email))
            {
                return Json(new { success = false, message = "Phiên đăng ký đã hết hạn." });
            }

            // Generate new OTP
            var otp = GenerateOTP();
            HttpContext.Session.SetString("Reg_OTP", otp);
            HttpContext.Session.SetString("Reg_OTP_Time", DateTime.Now.AddMinutes(5).ToString());

            // Send OTP
            var sent = await _emailService.SendOTPEmailAsync(email, otp);
            
            if (sent)
            {
                return Json(new { success = true, message = $"Mã OTP mới đã được gửi đến {MaskEmail(email)}" });
            }
            
            return Json(new { success = false, message = "Không thể gửi mã OTP. Vui lòng thử lại." });
        }

        private string GenerateOTP()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return "";
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            
            var name = parts[0];
            if (name.Length <= 3)
                return name[0] + "***@" + parts[1];
            return name.Substring(0, 3) + "***@" + parts[1];
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Profile
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }
            
            var orders = await _context.Orders
                .Where(o => o.UserId == user.Id)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            
            ViewBag.Orders = orders;
            return View(user);
        }

        // GET: /Account/ChangePassword
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Đổi mật khẩu thành công!";
                return RedirectToAction("Profile");
            }

            AddErrors(result);
            return View(model);
        }

        // GET: /Account/UserList - Admin only
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UserList()
        {
            var users = await _context.Users.ToListAsync();
            var userList = new List<UserListViewModel>();
            
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserListViewModel
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    FullName = user.FullName,
                    Roles = roles.ToList()
                });
            }
            
            return View(userList);
        }

        // GET: /Account/EditUserRoles/5 - Admin only
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditUserRoles(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var allRoles = new List<string> { "Admin", "Employee", "Customer" };
            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new EditUserRolesViewModel
            {
                UserId = user.Id,
                UserEmail = user.Email ?? "",
                UserFullName = user.FullName,
                AllRoles = allRoles.Select(r => new SelectListItem
                {
                    Value = r,
                    Text = r,
                    Selected = userRoles.Contains(r)
                }).ToList()
            };

            return View(model);
        }

        // POST: /Account/EditUserRoles - Admin only
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUserRoles(EditUserRolesViewModel model)
        {
            if (string.IsNullOrEmpty(model.UserId))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return NotFound();
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var selectedRoles = model.SelectedRoles ?? new List<string>();

            // Remove roles not selected
            foreach (var role in currentRoles)
            {
                if (!selectedRoles.Contains(role))
                {
                    await _userManager.RemoveFromRoleAsync(user, role);
                }
            }

            // Add new roles
            foreach (var role in selectedRoles)
            {
                if (!currentRoles.Contains(role))
                {
                    await _userManager.AddToRoleAsync(user, role);
                }
            }

            TempData["Success"] = "Cập nhật quyền thành công!";
            return RedirectToAction(nameof(UserList));
        }

        // GET: /Account/AccessDenied
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }

    // View Models
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Họ tên là bắt buộc")]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [StringLength(100, ErrorMessage = "Mật khẩu phải có ít nhất {2} ký tự.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Mật khẩu cũ là bắt buộc")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu cũ")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
        [StringLength(100, ErrorMessage = "Mật khẩu phải có ít nhất {2} ký tự.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu mới")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class UserListViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }

    public class EditUserRolesViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public List<SelectListItem> AllRoles { get; set; } = new();
        public List<string>? SelectedRoles { get; set; }
    }
}
