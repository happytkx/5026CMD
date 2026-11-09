
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;

namespace RROMS.Pages.Admin
{
    [Authorize(Policy = "Admin")]
    public class SettingsModel : PageModel
    {
        private readonly ApplicationDbContext db;

        public SettingsModel(ApplicationDbContext context)
        {
            db = context;
        }

        // Reservation deposit
        [BindProperty]
        [Range(0, 10000,
            ErrorMessage = "Deposit must be between RM0 and RM10,000.")]
        public decimal DepositPerGuest { get; set; } = 50m;

        // Reservation party size
        [BindProperty]
        [Range(1, 12,
            ErrorMessage = "Minimum party size must be between 1 and 12.")]
        public int MinimumPartySize { get; set; } = 1;

        [BindProperty]
        [Range(1, 12,
            ErrorMessage = "Maximum party size must be between 1 and 12.")]
        public int MaximumPartySize { get; set; } = 12;

        // Indoor and outdoor minimum spending
        [BindProperty]
        [Range(0, 100000,
            ErrorMessage = "Minimum spending must be between RM0 and RM100,000.")]
        public decimal StandardMinimumSpendPerGuest { get; set; } = 50m;

        // VIP room: 6 to 8 guests
        [BindProperty]
        [Range(0, 100000,
            ErrorMessage = "VIP minimum spending must be between RM0 and RM100,000.")]
        public decimal VipMinimumSpendSmallGroup { get; set; } = 2400m;

        // VIP room: 9 to 12 guests
        [BindProperty]
        [Range(0, 100000,
            ErrorMessage = "VIP minimum spending must be between RM0 and RM100,000.")]
        public decimal VipMinimumSpendLargeGroup { get; set; } = 3600m;

        // Tax percentage entered by administrator
        [BindProperty]
        [Range(0, 100,
            ErrorMessage = "Tax rate must be between 0% and 100%.")]
        public decimal TaxPercentage { get; set; } = 6m;

        public async Task OnGetAsync()
        {
            RestaurantSetting? settings =
                await db.RestaurantSettings
                    .AsNoTracking()
                    .OrderBy(s => s.SettingID)
                    .FirstOrDefaultAsync();

            if (settings == null)
            {
                return;
            }

            DepositPerGuest = settings.DepositPerGuest;
            MinimumPartySize = settings.MinimumPartySize;
            MaximumPartySize = settings.MaximumPartySize;

            StandardMinimumSpendPerGuest =
                settings.StandardMinimumSpendPerGuest;

            VipMinimumSpendSmallGroup =
                settings.VipMinimumSpendSmallGroup;

            VipMinimumSpendLargeGroup =
                settings.VipMinimumSpendLargeGroup;

            TaxPercentage = settings.TaxRate * 100m;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Check that the minimum party size does not
            // exceed the maximum party size.
            if (MinimumPartySize > MaximumPartySize)
            {
                ModelState.AddModelError(
                    "",
                    "Minimum party size cannot exceed maximum party size.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            RestaurantSetting? settings =
                await db.RestaurantSettings
                    .OrderBy(s => s.SettingID)
                    .FirstOrDefaultAsync();

            if (settings == null)
            {
                ModelState.AddModelError(
                    "",
                    "Restaurant settings could not be found.");

                return Page();
            }

            // Save reservation deposit configuration.
            settings.DepositPerGuest = DepositPerGuest;

            // Save party size limits.
            settings.MinimumPartySize = MinimumPartySize;
            settings.MaximumPartySize = MaximumPartySize;

            // Save minimum spending configuration.
            settings.StandardMinimumSpendPerGuest =
                StandardMinimumSpendPerGuest;

            settings.VipMinimumSpendSmallGroup =
                VipMinimumSpendSmallGroup;

            settings.VipMinimumSpendLargeGroup =
                VipMinimumSpendLargeGroup;

            // Convert percentage to decimal fraction.
            // Example: 8% is stored as 0.08.
            settings.TaxRate = TaxPercentage / 100m;

            await db.SaveChangesAsync();

            TempData["Success"] =
                "Restaurant settings saved successfully.";

            return RedirectToPage();
        }
    }
}
