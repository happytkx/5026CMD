// Displays the English menu name while retaining the original name/price snapshots
// in historical order lines. Also upgrades previously seeded menu items in place.
namespace RROMS.Services;

public static class MenuDisplayNames
{
    private static readonly IReadOnlyDictionary<string, string> EnglishByOriginalName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bisque de Homard"] = "Lobster Bisque",
            ["Velouté de Champignons"] = "Creamy Mushroom Soup",
            ["Soupe à l'Oignon"] = "French Onion Soup",
            ["Escargots de Bourgogne"] = "Burgundy-Style Garlic Butter Snails",
            ["Saint-Jacques"] = "Pan-Seared Scallops",
            ["Salade de Chèvre Chaud"] = "Warm Goat Cheese Salad",
            ["Magret de Canard"] = "Pan-Seared Duck Breast",
            ["Filet de Bœuf"] = "Beef Fillet",
            ["Carré d'Agneau"] = "Herb-Crusted Rack of Lamb",
            ["Saumon Poêlé"] = "Pan-Seared Salmon",
            ["Suprême de Poulet"] = "Roasted Chicken Breast",
            ["Gnocchis Parisiens"] = "Parisian-Style Gnocchi",
            ["Crème Brûlée"] = "Crème brûlée",
            ["Caramelised Vanilla Custard"] = "Crème brûlée",
            ["Tarte au Citron"] = "French Lemon Tart",
            ["Profiteroles au Chocolat"] = "Chocolate Cream Puffs",
            ["Citron & Thym"] = "Lemon & Thyme Cooler",
            ["Pêche Blanche"] = "White Peach Cooler",
            ["Fleur de Sureau"] = "Elderflower Citrus Cooler",
            ["Eau Minérale"] = "Still Mineral Water",
            ["Eau Pétillante"] = "Sparkling Mineral Water",
        };

    public static string InEnglish(string name) =>
        EnglishByOriginalName.TryGetValue(name, out var englishName)
            ? englishName
            : name;
}
