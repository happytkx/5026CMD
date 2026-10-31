using RROMS.Models;

namespace RROMS.Services;

// Show one dining session per reservation while retaining each kitchen round internally.
public static class DiningOrderPresentation
{
    public static string Status(IEnumerable<CustomerOrder> orders)
    {
        var active = orders.Where(o => o.Status != "Cancelled").SelectMany(o => o.Lines)
            .Where(line => !line.IsCancelled)
            .Select(line => line.KitchenStatus).Distinct().ToList();

        if (active.Count == 0)
            return "Cancelled";
        if (active.All(status => status is "Served" or "Completed"))
            return "Served";
        return active.Count == 1 ? active[0] : "In progress";
    }

    public static string RoundStatus(CustomerOrder order)
    {
        if (order.Status == "Cancelled") return "Cancelled";
        var active = order.Lines.Where(line => !line.IsCancelled)
            .Select(line => line.KitchenStatus).Distinct().ToList();
        if (active.Count == 0)
            return "Cancelled";
        return active.Count == 1 ? active[0] : "In progress";
    }

    public static decimal Total(IEnumerable<CustomerOrder> orders)
        => orders.Where(o => o.Status != "Cancelled").Sum(o => o.Total);

    public static int ActiveItemCount(IEnumerable<CustomerOrder> orders)
        => orders.Where(o => o.Status != "Cancelled")
            .SelectMany(o => o.Lines.Where(line => !line.IsCancelled))
            .Sum(line => line.Quantity);
}
