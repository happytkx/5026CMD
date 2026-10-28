// Application startup: registers the database, customer/staff access policies, reservation services and background workers, then maps the Razor pages.
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/AccessDenied";
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", p => p.RequireRole("Customer"));
    options.AddPolicy("Staff", p => p.RequireRole("Restaurant Staff", "Staff", "Administrator"));
    options.AddPolicy("Admin", p => p.RequireRole("Administrator"));
});
builder.Services.AddSingleton<IPasswordHasher<Customer>, PasswordHasher<Customer>>();
builder.Services.AddScoped<ReservationOrderBillingSource, OrderBillingSource>();
builder.Services.AddScoped<OrderBillingIntegrationService>();
builder.Services.AddScoped<TableAllocationService>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<ReportingService>();
builder.Services.AddScoped<OccupancyReportingService>();
builder.Services.AddScoped<BillingService>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<ReservationEmailService>();
builder.Services.AddHostedService<ReservationEmailWorker>();
builder.Services.AddHostedService<ReservationExpiryWorker>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// Session holds each signed-in customer's menu basket until the order is placed.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Initialize database and seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    DbInitializer.Initialize(context, app.Environment.IsDevelopment());
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
