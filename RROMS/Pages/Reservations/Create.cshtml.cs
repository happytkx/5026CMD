// Booking page handlers: search availability, validate the chosen time, load account contacts and collect requests before creating a pending reservation.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Services;
using RROMS.Data;
using RROMS.Models;
using Microsoft.EntityFrameworkCore;
namespace RROMS.Pages.Reservations;

[Authorize(Policy = "Customer")]
public class CreateModel(ReservationService service, ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true), Range(1, 12)] public int PartySize { get; set; } = 2;
    [BindProperty(SupportsGet = true)] public string Area { get; set; } = "Indoor";
    [BindProperty(SupportsGet = true), DataType(DataType.Date)] public DateTime Date { get; set; } = ReservationPolicy.Now.Date.AddDays(1);
    [BindProperty(SupportsGet = true)] public string Time { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Stage { get; set; } = "Search";
    [BindProperty, StringLength(200)]
    public string? Allergies
    {
        get; set;
    }
    [BindProperty] public List<string> Preferences { get; set; } = [];
    public Customer Guest { get; set; } = new();
    public RestaurantSetting CurrentPolicy { get; private set; } = new();
    public Reservation Summary => new() { Customer = Guest, PartySize = PartySize, PreferredDiningOption = Area, ReservationDate = Date.Date, StartTime = TimeSpan.TryParse(Time, out var t) ? t : TimeSpan.Zero, DepositAmount = PartySize * CurrentPolicy.DepositPerGuest, MinimumSpend = ReservationPolicy.MinimumSpend(PartySize, Area, CurrentPolicy) };
    [BindProperty, StringLength(1000)]
    public string? Requests
    {
        get; set;
    }
    [BindProperty]
    public bool AcceptPolicy
    {
        get; set;
    }
    public List<DateTime> Slots { get; set; } = [];
    public async Task OnGetAsync()
    {
        PartySize = Math.Clamp(PartySize, 1, 12);
        if (!ReservationPolicy.Areas(PartySize).Contains(Area))
            Area = ReservationPolicy.Areas(PartySize).FirstOrDefault() ?? "Indoor";
        // Clear the old area submitted by the auto-refreshing filter. Tag helpers
        // otherwise reuse that old value in the hidden booking form.
        ModelState.Clear();
        if (Stage is not ("Search" or "Availability" or "Guest"))
            Stage = "Search";
        await Load();
        if (Stage == "Guest" && (!TimeSpan.TryParse(Time, out var t) || !Slots.Any(s => s.TimeOfDay == t)))
        {
            Stage = "Availability";
            ModelState.AddModelError("", "That arrival time is no longer available. Please choose another.");
        }
    }
    private async Task Load()
    {
        CurrentPolicy = await db.RestaurantSettings.AsNoTracking().OrderBy(s => s.SettingID).FirstOrDefaultAsync() ?? new RestaurantSetting();
        Slots = await service.Slots(PartySize, Area, Date);
        Guest = await db.Customers.AsNoTracking().SingleAsync(c => c.CustomerID == ReservationIdentity.CustomerId(User));
    }
    public async Task<IActionResult> OnPostAsync()
    {
        Stage = "Guest";
        if (!AcceptPolicy)
            ModelState.AddModelError(nameof(AcceptPolicy), "Please read and accept the Reservation Policy before continuing.");
        if (!TimeSpan.TryParse(Time, out var time) || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
            ModelState.AddModelError("", "Choose a valid time.");
        if (ModelState.IsValid)
        {
            try
            {
                var allowed = new[] { "Vegetarian", "Vegan", "No Pork", "No Beef", "No Chicken" };
                var notes = string.Join("\n", new[] { Preferences.Any() ? "Dietary preferences: " + 
                    string.Join(", ", Preferences.Where(allowed.Contains).Distinct()) : null, string.IsNullOrWhiteSpace(Allergies) ? null : "Allergies: " + 
                    Allergies, Requests }.Where(n => !string.IsNullOrWhiteSpace(n)));
                if (notes.Length > 1000)
                    throw new InvalidOperationException("Please keep your combined dining requests within 1,000 characters.");
                var booking = await service.Create(ReservationIdentity.CustomerId(User), PartySize, Area, Date.Date.Add(time), notes);
                return RedirectToPage("Details", new
                {
                    id = booking.ReservationID,
                    view = "Payment"
                });
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException) { ModelState.AddModelError("", "Availability changed. Please try again."); }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 1205)
            {
                ModelState.AddModelError("", "Another booking was processed at the same time. Please retry.");
            }
        }
        await Load();
        return Page();
    }
}
