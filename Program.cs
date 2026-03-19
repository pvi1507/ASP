using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BC_ASP.Data;
using BC_ASP.Models;
using BC_ASP.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DataProtection services (fix session cookie error)
builder.Services.AddDataProtection();

// Add Session services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// Configure DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity - FIXED OTP PASSWORD POLICY
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.ClaimsIdentity.UserNameClaimType = "FullName";

    // Password settings - Nới lỏng cho OTP chỉ số
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    
    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure Application Cookie
builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    
    context.Database.EnsureCreated();
    
    // Seed Roles
    if (!roleManager.RoleExistsAsync("Admin").Result)
    {
        roleManager.CreateAsync(new IdentityRole("Admin")).Wait();
    }
    if (!roleManager.RoleExistsAsync("Employee").Result)
    {
        roleManager.CreateAsync(new IdentityRole("Employee")).Wait();
    }
    if (!roleManager.RoleExistsAsync("Customer").Result)
    {
        roleManager.CreateAsync(new IdentityRole("Customer")).Wait();
    }
    
    // Seed Admin User
    var adminEmail = "admin@bcasp.com";
    if (userManager.FindByEmailAsync(adminEmail).Result == null)
    {
        var adminUser = new ApplicationUser
        {
            UserName = "Admin",
            Email = adminEmail,
            FullName = "Administrator",
            EmailConfirmed = true
        };
        var result = userManager.CreateAsync(adminUser, "Admin123!").Result;
        if (result.Succeeded)
        {
            userManager.AddToRoleAsync(adminUser, "Admin").Wait();
        }
    }
}

app.Run();
