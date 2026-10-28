// Basket and checkout: a customer can place an order only against their own reservation that staff have marked Seated, with prices re-read from the menu.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RROMS.Data;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Pages.Orders;

[Authorize]
public class CheckoutModel(ApplicationDbContext db) : PageModel
{
    // Service tax added to dine-in orders.
    private const decimal TaxRate = .06m;

    public record BasketLine(int Id, string Name, decimal Price, int Quantity);
    public List<BasketLine> Lines { get; set; } = [];
    public List<Reservation> EligibleReservations { get; set; } = [];
    public decimal Subtotal => Lines.Sum(l => l.Price * l.Quantity);
    public decimal Tax => Math.Round(Subtotal * TaxRate, 2);
    public decimal Total => Subtotal + Tax;
    [BindProperty]
    public int ReservationId
    {
        get; set;
    }
    [BindProperty, StringLength(500, ErrorMessage = "Order notes must be 500 characters or fewer.")]
    public string? Notes
    {
        get; set;
    }

    private int CustomerId => ReservationIdentity.CustomerId(User);

    private async Task Load()
    {
        var basket = Basket.Read(HttpContext.Session);
        var items = await db.MenuItems.AsNoTracking().Where(i => basket.Keys.Contains(i.MenuItemID) && i.IsAvailable).ToListAsync();
        Lines = items.Select(i => new BasketLine(i.MenuItemID, i.Name, i.Price, Math.Clamp(basket[i.MenuItemID], 1, Basket.MaxQuantity))).ToList();
        EligibleReservations = await db.Reservations.AsNoTracking()
            .Where(r => r.CustomerID == CustomerId && r.ReservationStatus == "Seated")
            .OrderByDescending(r => r.ReservationDate).ThenByDescending(r => r.StartTime)
            .ToListAsync();
        if (ReservationId == 0 && EligibleReservations.Count > 0)
            ReservationId = EligibleReservations[0].ReservationID;
    }

    public async Task OnGetAsync() => await Load();

    public IActionResult OnPostChange(int itemId, int quantity)
    {
        var basket = Basket.Read(HttpContext.Session);
        if (quantity <= 0)
            basket.Remove(itemId);
        else
            basket[itemId] = Math.Min(Basket.MaxQuantity, quantity);
        Basket.Write(HttpContext.Session, basket);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPlaceAsync()
    {
        await Load();
        if (Lines.Count == 0)
        {
            TempData["Error"] = "Your basket is empty.";
            return RedirectToPage();
        }
        // Re-checked in the database: ownership and the Seated status are never trusted from the form.
        var reservation = await db.Reservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReservationID == ReservationId && r.CustomerID == CustomerId && r.ReservationStatus == "Seated");
        if (reservation == null)
        {
            TempData["Error"] = "Dine-in orders can only be placed through your active reservation after staff have checked you in and marked you Seated.";
            return RedirectToPage();
        }
        if (!ModelState.IsValid)
            return Page();

        var order = new CustomerOrder
        {
            CustomerID = CustomerId,
            ReservationID = reservation.ReservationID,
            CreatedAt = ReservationPolicy.Now,
            Subtotal = Subtotal,
            Tax = Tax,
            Total = Total,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            Status = "Received",
            Lines = Lines.Select(l => new OrderLine { MenuItemID = l.Id, ItemName = l.Name, UnitPrice = l.Price, Quantity = l.Quantity, LineTotal = l.Price * l.Quantity }).ToList()
        };
        db.Orders.Add(order);
        db.CustomerNotifications.Add(new CustomerNotification
        {
            CustomerID = CustomerId,
            Title = "Order received",
            Message = $"Your order {order.Reference} for table {reservation.TableID} has been received. Total: RM {order.Total:0.00}.",
            Type = "Order",
            RelatedOrder = order,
            CreatedAt = order.CreatedAt
        });
        await db.SaveChangesAsync();
        Basket.Clear(HttpContext.Session);
        TempData["Success"] = $"Order {order.Reference} has been placed. Follow its progress in My orders.";
        return RedirectToPage("/Orders/Index");
    }
}
