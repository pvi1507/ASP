using Microsoft.AspNetCore.Http;
using System.Text.Json;
using BC_ASP.Data;
using BC_ASP.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using BC_ASP.Extensions;

namespace BC_ASP.Extensions
{
    public static class CartSessionExtensions
    {
        public static async Task LoadUserCartToSession(this ISession session, ApplicationDbContext context, string userId)
        {
            var dbCart = await context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            session.Set("Cart", dbCart);
        }
    }
}
