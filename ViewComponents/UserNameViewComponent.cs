using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BC_ASP.Data;
using BC_ASP.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

public class UserNameViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserNameViewComponent(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity.IsAuthenticated)
        {
        var user = await _userManager.GetUserAsync((ClaimsPrincipal)User);
            if (user != null)
            {
                ViewBag.UserFullName = user!.FullName;
            }
        }
        return View();
    }
}
