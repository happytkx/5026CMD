using RROMS.Models;

namespace RROMS.Services;

// Older catalogues predate image uploads. Keep administrator-supplied photos first.
public static class MenuImages
{
    public const string Fallback = "/images/celeste-indoor.png";
    public static string Url(MenuItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.ImageUrl)) return item.ImageUrl;
        var filename = item.Name switch
        {
            "Vanilla crème brûlée" or "Crème Brûlée" => "creme-brulee-dessert.png",
            "Herb-roasted chicken" or "Suprême de Poulet" => "chicken-main.png",
            "Iced peach tea" or "Pêche Blanche" => "peach-beverage.png",
            "Sparkling water" or "Eau Pétillante" => "sparkling-beverage.png",
            "Seared scallops" or "Saint-Jacques" => "scallops-appertizer.png",
            "Bisque de Homard" => "lobster-soup.png",
            "Velouté de Champignons" => "mushroom-soup.png",
            "Soupe à l'Oignon" => "onion-soup.png",
            "Escargots de Bourgogne" => "escargots-appertizer.png",
            "Salade de Chèvre Chaud" => "salad-appertizer.png",
            "Magret de Canard" => "duck-main.png",
            "Filet de Bœuf" => "beef-main.png",
            "Carré d'Agneau" => "lamb-main.png",
            "Saumon Poêlé" => "salmon-main.png",
            "Gnocchis Parisiens" => "gnocchi-main.png",
            "Tarte au Citron" => "lemon-tart-dessert.png",
            "Profiteroles au Chocolat" => "cream-puffs-dessert.png",
            "Citron & Thym" => "lemonade-beverage.png",
            "Fleur de Sureau" => "elderflower-beverage.png",
            "Eau Minérale" => "water-beverage.png",
            "Truffle mushroom risotto" => "truffle-mushroom-risotto.png",
            "Chocolate fondant" => "chocolate-fondant.png",
            _ => null
        };
        return filename == null ? Fallback : "/images/menu/" + filename;
    }
    public static string Alt(MenuItem item) => Url(item) == Fallback ? "Dining at Céleste" : item.Name;
}
