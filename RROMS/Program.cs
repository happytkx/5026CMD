// Application startup: registers the database, customer/staff access policies, reservation services and background workers, then maps the Razor pages.
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.LoginPath = "/Account/Login"; options.AccessDeniedPath = "/Account/Denied";
        options.Cookie.HttpOnly = true; options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8); options.SlidingExpiration = true;
        options.EventsType = typeof(CustomerCookieEvents);
    });

builder.Services.AddScoped<IPasswordHasher<Customer>, PasswordHasher<Customer>>();
builder.Services.AddScoped<CustomerAccountService>();
builder.Services.AddScoped<IPasswordHasher<StaffAccount>, PasswordHasher<StaffAccount>>();
builder.Services.AddScoped<StaffAccountService>();
builder.Services.AddScoped<IPasswordHasher<Administrator>, PasswordHasher<Administrator>>();
builder.Services.AddScoped<AdministratorAccountService>();
builder.Services.AddScoped<CustomerCookieEvents>();
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("accounts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("staff-accounts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", p => p.RequireRole("Customer"));
    options.AddPolicy("Staff", p => p.RequireRole("Restaurant Staff", "Staff", "Administrator"));
    options.AddPolicy("Admin", p => p.RequireRole("Administrator"));
});
builder.Services.AddScoped<ReservationOrderBillingSource, OrderBillingSource>();
builder.Services.AddScoped<OrderBillingIntegrationService>();
builder.Services.AddScoped<TableAllocationService>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<ReportingService>();
builder.Services.AddScoped<BusinessAnalyticsService>();
builder.Services.AddScoped<OccupancyReportingService>();
builder.Services.AddScoped<BillingService>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<ReservationEmailService>();
builder.Services.AddHostedService<ReservationEmailWorker>();
builder.Services.AddHostedService<ReservationExpiryWorker>();

var databaseConnection = await DevelopmentLocalDb.Resolve(
    builder.Configuration.GetConnectionString("DefaultConnection"), builder.Environment.IsDevelopment());
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(databaseConnection));

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

    Console.WriteLine("================================");
    Console.WriteLine("Database: " +
        context.Database.GetDbConnection().Database);
    Console.WriteLine("Server: " +
        context.Database.GetDbConnection().DataSource);
    Console.WriteLine("================================");

    DbInitializer.Initialize(context, app.Environment.IsDevelopment());

    if (app.Environment.IsDevelopment() &&
        builder.Configuration.GetValue<bool>("DemoData:Enabled"))
    {
        await DevelopmentDataSeeder.Seed(context, builder.Configuration);
    }
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
