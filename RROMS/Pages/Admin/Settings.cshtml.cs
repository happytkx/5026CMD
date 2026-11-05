
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

        [BindProperty]
        [Range(0, 10000)]
        public decimal DepositPerGuest { get; set; } = 50m;

        [BindProperty]
        [Range(1, 12)]
        public int MinimumPartySize { get; set; } = 1;

        [BindProperty]
        [Range(1, 12)]
        public int MaximumPartySize { get; set; } = 12;

        [BindProperty]
        [Range(0, 100)]
        public decimal TaxPercentage { get; set; } = 6m;

        public async Task OnGetAsync()
        {
            RestaurantSetting? settings = await db.RestaurantSettings
                .OrderBy(s => s.SettingID)
                .FirstOrDefaultAsync();

            if (settings != null)
            {
                DepositPerGuest = settings.DepositPerGuest;
                MinimumPartySize = settings.MinimumPartySize;
                MaximumPartySize = settings.MaximumPartySize;
                TaxPercentage = settings.TaxRate * 100m;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
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

            RestaurantSetting? settings = await db.RestaurantSettings
                .OrderBy(s => s.SettingID)
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                ModelState.AddModelError(
                    "",
                    "Restaurant settings could not be found.");

                return Page();
            }

            settings.DepositPerGuest = DepositPerGuest;
            settings.MinimumPartySize = MinimumPartySize;
            settings.MaximumPartySize = MaximumPartySize;
            settings.TaxRate = TaxPercentage / 100m;

            await db.SaveChangesAsync();

            TempData["Success"] = "Restaurant settings saved successfully.";

            return RedirectToPage();
        }
    }
}
