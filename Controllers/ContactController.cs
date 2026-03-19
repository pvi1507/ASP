using Microsoft.AspNetCore.Mvc;

namespace BC_ASP.Controllers
{
    public class ContactController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult SendContact(string name, string email, string phone, string subject, string message)
        {
            // Xử lý gửi liên hệ
            // Trong thực tế, lưu vào database hoặc gửi email
            
            TempData["Success"] = "Cảm ơn bạn đã liên hệ! Chúng tôi sẽ phản hồi trong thời gian sớm nhất.";
            return RedirectToAction("Index");
        }
    }
}
