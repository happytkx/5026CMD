// Application startup: registers the database, customer/staff access policies, reservation services and background workers, then maps the Razor pages.
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => { options.LoginPath = "/Access"; options.AccessDeniedPath = "/Access"; });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", p => p.RequireRole("Customer"));
    options.AddPolicy("Staff", p => p.RequireRole("Restaurant Staff", "Staff"));
});
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

builder.Services.AddSession();

var app = builder.Build();

// Initialize database and seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    DbInitializer.Initialize(context);
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
