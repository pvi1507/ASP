using Microsoft.AspNetCore.Mvc;
using BC_ASP.Data;
using BC_ASP.Models;

namespace BC_ASP.Controllers
{
    public class ContactController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContactController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendContact(string name, string email, string phone, string subject, string message)
        {
            var contactMessage = new ContactMessage
            {
                Name = name,
                Email = email,
                Phone = phone,
                Subject = subject,
                Message = message,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.ContactMessages.Add(contactMessage);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cảm ơn bạn đã liên hệ! Chúng tôi sẽ phản hồi trong thời gian sớm nhất.";
            return RedirectToAction("Index");
        }
    }
}

