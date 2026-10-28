// Session-backed menu basket: dish ID to quantity, shared by the Menu and Checkout pages. Prices are always re-read from the database at checkout.
using System.Text.Json;

namespace RROMS.Services;

public static class Basket
{
    public const int MaxQuantity = 20;
    private const string Key = "cart";

    public static Dictionary<int, int> Read(ISession session)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, int>>(session.GetString(Key) ?? "") ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static void Write(ISession session, Dictionary<int, int> basket) =>
        session.SetString(Key, JsonSerializer.Serialize(basket));

    public static void Clear(ISession session) => session.Remove(Key);
}
