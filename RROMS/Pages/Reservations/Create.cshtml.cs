
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Reservations;

[Authorize(Policy = "Customer")]
public class CreateModel(
    ReservationService service,
    ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    [Range(1, 12)]
    public int PartySize { get; set; } = 2;

    [BindProperty(SupportsGet = true)]
    public string Area { get; set; } = "Indoor";

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } =
        ReservationPolicy.Now.Date.AddDays(1);

    [BindProperty(SupportsGet = true)]
    public string Time { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Stage { get; set; } = "Search";

    [BindProperty]
    [StringLength(200)]
    public string? Allergies { get; set; }

    [BindProperty]
    public List<string> Preferences { get; set; } = [];

    [BindProperty]
    [StringLength(1000)]
    public string? Requests { get; set; }

    [BindProperty]
    public bool AcceptPolicy { get; set; }

    public Customer Guest { get; set; } = new();

    public List<DateTime> Slots { get; set; } = [];

    // Current administrator-configured restaurant settings.
    public RestaurantSetting Settings { get; private set; } = new();

    // Used by the customer reservation summary.
    public Reservation Summary => new()
    {
        Customer = Guest,
        PartySize = PartySize,
        PreferredDiningOption = Area,
        ReservationDate = Date.Date,
        StartTime = TimeSpan.TryParse(Time, out var t)
            ? t
            : TimeSpan.Zero,

        DepositAmount = PartySize * Settings.DepositPerGuest,

        MinimumSpend = ReservationPolicy.MinimumSpend(
            PartySize,
            Area,
            Settings)
    };

    public async Task OnGetAsync()
    {
        Settings = await service.Settings();

        if (Stage is not ("Search" or "Availability" or "Guest"))
        {
            Stage = "Search";
        }

        // On the initial search page, correct invalid guest counts.
        // On later stages, reject them instead of silently
        // changing the customer's selection.
        if (Stage == "Search")
        {
            PartySize = Math.Clamp(
                PartySize,
                Settings.MinimumPartySize,
                Settings.MaximumPartySize);

            var allowedAreas = ReservationPolicy.Areas(PartySize);

            if (!allowedAreas.Contains(Area))
            {
                Area = allowedAreas.FirstOrDefault() ?? "Indoor";
            }

            ModelState.Clear();
        }
        else if (!IsValidSelection())
        {
            Stage = "Search";

            PartySize = Math.Clamp(
                PartySize,
                Settings.MinimumPartySize,
                Settings.MaximumPartySize);

            Area = ReservationPolicy.Areas(PartySize)
                .FirstOrDefault() ?? "Indoor";

            ModelState.Clear();

            ModelState.AddModelError(
                "",
                "Your selected guest count or dining area is not " +
                "available under the current restaurant settings.");
        }

        await LoadAsync();

        if (Stage == "Guest" &&
            (!TimeSpan.TryParse(Time, out var selectedTime) ||
             !Slots.Any(s => s.TimeOfDay == selectedTime)))
        {
            Stage = "Availability";

            ModelState.AddModelError(
                "",
                "That arrival time is no longer available. " +
                "Please choose another.");
        }
    }

    private bool IsValidSelection()
    {
        return PartySize >= Settings.MinimumPartySize &&
               PartySize <= Settings.MaximumPartySize &&
               ReservationPolicy.Areas(PartySize).Contains(Area);
    }

    private async Task LoadAsync()
    {
        Settings = await service.Settings();

        Guest = await db.Customers
            .AsNoTracking()
            .SingleAsync(c =>
                c.CustomerID == ReservationIdentity.CustomerId(User));

        Slots = [];

        if (IsValidSelection() &&
            Date.Date >= ReservationPolicy.Now.Date &&
            Date.Date <= ReservationPolicy.Now.Date.AddYears(2))
        {
            Slots = await service.Slots(
                PartySize,
                Area,
                Date.Date);
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Stage = "Guest";

        Settings = await service.Settings();

        if (!IsValidSelection())
        {
            ModelState.AddModelError(
                nameof(PartySize),
                "The guest count or dining area is not allowed " +
                "under the current restaurant settings.");
        }

        if (!AcceptPolicy)
        {
            ModelState.AddModelError(
                nameof(AcceptPolicy),
                "Please read and accept the Reservation Policy " +
                "before continuing.");
        }

        if (!TimeSpan.TryParse(Time, out var selectedTime) ||
            selectedTime < TimeSpan.Zero ||
            selectedTime >= TimeSpan.FromDays(1))
        {
            ModelState.AddModelError(
                nameof(Time),
                "Choose a valid arrival time.");
        }

        if (Date.Date < ReservationPolicy.Now.Date ||
            Date.Date > ReservationPolicy.Now.Date.AddYears(2))
        {
            ModelState.AddModelError(
                nameof(Date),
                "Choose a valid future reservation date.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                string[] allowedPreferences =
                [
                    "Vegetarian",
                    "Vegan",
                    "No Pork",
                    "No Beef",
                    "No Chicken"
                ];

                var validPreferences = Preferences
                    .Where(allowedPreferences.Contains)
                    .Distinct()
                    .ToList();

                var notes = string.Join(
                    "\n",
                    new[]
                    {
                        validPreferences.Count > 0
                            ? "Dietary preferences: " +
                              string.Join(", ", validPreferences)
                            : null,

                        string.IsNullOrWhiteSpace(Allergies)
                            ? null
                            : "Allergies: " + Allergies.Trim(),

                        string.IsNullOrWhiteSpace(Requests)
                            ? null
                            : Requests.Trim()
                    }.Where(n => !string.IsNullOrWhiteSpace(n)));

                if (notes.Length > 1000)
                {
                    throw new InvalidOperationException(
                        "Please keep your combined dining requests " +
                        "within 1,000 characters.");
                }

                var booking = await service.Create(
                    ReservationIdentity.CustomerId(User),
                    PartySize,
                    Area,
                    Date.Date.Add(selectedTime),
                    notes);

                return RedirectToPage(
                    "Details",
                    new
                    {
                        id = booking.ReservationID,
                        view = "Payment"
                    });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    "",
                    "Availability changed. Please try again.");
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when (ex.Number == 1205)
            {
                ModelState.AddModelError(
                    "",
                    "Another booking was processed at the same " +
                    "time. Please retry.");
            }
        }

        await LoadAsync();

        return Page();
    }
}
