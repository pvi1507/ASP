using Microsoft.AspNetCore.Identity;

namespace BC_ASP.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

