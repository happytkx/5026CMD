// Applies pending database migrations and seeds sample customers, dining areas, tables, opening hours, menu and announcements when the corresponding tables are empty.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context, bool demoAccounts = false)
        {
            context.Database.Migrate();

            // Seed Customer
            if (!context.Customers.Any())
            {
                var customer = new Customer
                {
                    CustomerName = "Test Customer",
                    Email = "test@gmail.com",
                    PhoneNo = "0123456789"
                };

                context.Customers.Add(customer);

                context.SaveChanges();
            }

            // Seed Dining Areas
            if (!context.DiningAreas.Any())
            {
                var diningAreas = new DiningArea[]
                {
                    new DiningArea
                    {
                        AreaType = "Indoor",
                    },

                    new DiningArea
                    {
                        AreaType = "Outdoor",
                    },

                    new DiningArea
                    {
                        AreaType = "VIP Room",
                    }
                };

                context.DiningAreas.AddRange(diningAreas);

                context.SaveChanges();
            }

            // Seed Restaurant Tables
            if (!context.RestaurantTables.Any())
            {
                var tables = new RestaurantTable[]
                {
                    new RestaurantTable
                    {
                        TableID = "T01",
                        DiningAreaID = 1,
                        Capacity = 2,
                        Status = "Available"
                    },

                    new RestaurantTable
                    {
                        TableID = "T02",
                        DiningAreaID = 1,
                        Capacity = 4,
                        Status = "Available"
                    },

                    new RestaurantTable
                    {
                        TableID = "T03",
                        DiningAreaID = 2,
                        Capacity = 6,
                        Status = "Available"
                    },

                    new RestaurantTable
                    {
                        TableID = "VIP01",
                        DiningAreaID = 3,
                        Capacity = 8,
                        Status = "Available"
                    },

                    new RestaurantTable
                    {
                        TableID = "VIP02",
                        DiningAreaID = 3,
                        Capacity = 12,
                        Status = "Available"
                    }
                };

                context.RestaurantTables.AddRange(tables);

                context.SaveChanges();
            }

            // Seed Restaurant Setting
            if (!context.RestaurantSettings.Any())
            {
                var setting = new RestaurantSetting();

                context.RestaurantSettings.Add(setting);

                context.SaveChanges();
            }

            // Seed demo staff and administrator accounts (Development only)
            if (demoAccounts)
            {
                if (!context.Administrators.Any(a => a.Email == "admin@celeste.local"))
                {
                    var admin = new Administrator { Name = "Celeste Administrator", Email = "admin@celeste.local", NormalizedEmail = "ADMIN@CELESTE.LOCAL", IsDemo = true };
                    admin.PasswordHash = new PasswordHasher<Administrator>().HashPassword(admin, "Admin123!");
                    context.Administrators.Add(admin);
                }
                if (!context.StaffAccounts.Any(a => a.Email == "staff@celeste.local"))
                {
                    var staff = new StaffAccount { StaffName = "Restaurant Staff", Email = "staff@celeste.local", NormalizedEmail = "STAFF@CELESTE.LOCAL", IsDemo = true };
                    staff.PasswordHash = new PasswordHasher<StaffAccount>().HashPassword(staff, "Staff123!");
                    context.StaffAccounts.Add(staff);
                }

                context.SaveChanges();
            }

            // Seed only missing official Céleste dishes, including on databases that
            // already contain the previous sample menu. Preserve existing item IDs,
            // administrator edits, order lines and billing history.
            // Helper for creating menu items.
            static MenuItem Dish(
                            string name,
                            string description,
                            decimal price,
                            string category,
                            string protein,
                            string allergens,
                            string dietaryTags = "No pork",
                            bool popular = false)
                        {
                            return new MenuItem
                            {
                                Name = name,
                                Description = description,
                                Price = price,
                                Category = category,
                                ProteinType = protein,
                                Allergens = allergens,
                                DietaryTags = dietaryTags,
                                IsPopular = popular,
                                IsAvailable = true,
                                SpiceLevel = 0
                            };
                        }

                        // Premium Céleste menu catalogue.
                        var officialMenu = new List<MenuItem>
            {
                // SOUPS
                Dish(
                    "Lobster Bisque",
                    "Velvety lobster bisque with crème fraîche and chive oil.",
                    58m, "Soups", "Seafood", "Shellfish, Milk",
                    "No pork, No beef, No chicken", true),

                Dish(
                    "Creamy Mushroom Soup",
                    "Silky wild mushroom soup finished with truffle oil.",
                    48m, "Soups", "Vegetarian", "Milk",
                    "Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "French Onion Soup",
                    "Slow-cooked onion soup with vegetable stock and Gruyère toast.",
                    46m, "Soups", "Vegetarian", "Milk, Wheat",
                    "Vegetarian, No pork, No beef, No chicken"),

                // APPETIZERS
                Dish(
                    "Burgundy-Style Garlic Butter Snails",
                    "Burgundy-style escargots in garlic and parsley butter.",
                    52m, "Appetizers", "Molluscs", "Milk, Molluscs"),

                Dish(
                    "Pan-Seared Scallops",
                    "Seared scallops with cauliflower purée and citrus butter.",
                    72m, "Appetizers", "Seafood", "Shellfish, Milk",
                    "No pork, No beef, No chicken", true),

                Dish(
                    "Warm Goat Cheese Salad",
                    "Warm goat cheese salad with baby leaves, walnuts and honey.",
                    46m, "Appetizers", "Vegetarian", "Milk, Tree nuts",
                    "Vegetarian, No pork, No beef, No chicken"),

                // MAIN COURSES
                Dish(
                    "Pan-Seared Duck Breast",
                    "Pan-seared duck breast with seasonal vegetables and a rich jus.",
                    138m, "Main Course", "Duck", "Milk",
                    "No pork", true),

                Dish(
                    "Beef Fillet",
                    "Tender beef fillet with potato purée and peppercorn sauce.",
                    178m, "Main Course", "Beef", "Milk",
                    "No pork", true),

                Dish(
                    "Herb-Crusted Rack of Lamb",
                    "Herb-crusted lamb rack with roasted vegetables and rosemary jus.",
                    188m, "Main Course", "Lamb", "Wheat, Milk",
                    "No pork"),

                Dish(
                    "Pan-Seared Salmon",
                    "Pan-seared salmon with seasonal greens and lemon butter sauce.",
                    118m, "Main Course", "Seafood", "Fish, Milk",
                    "No pork, No beef, No chicken"),

                Dish(
                    "Roasted Chicken Breast",
                    "Roasted chicken supreme with garden herbs and potato gratin.",
                    98m, "Main Course", "Chicken", "Milk",
                    "No pork"),

                Dish(
                    "Parisian-Style Gnocchi",
                    "Parisian-style gnocchi with wild mushrooms and creamy Parmesan sauce.",
                    88m, "Main Course", "Vegetarian", "Milk, Egg, Wheat",
                    "Vegetarian, No pork, No beef, No chicken"),

                // DESSERTS
                Dish(
                    "Caramelised Vanilla Custard",
                    "Classic vanilla custard beneath a crisp caramelised sugar crust, served with fresh berries.",
                    38m, "Desserts", "Vegetarian", "Milk, Egg",
                    "Vegetarian, No pork, No beef, No chicken", true),

                Dish(
                    "French Lemon Tart",
                    "French lemon tart with buttery pastry and delicate citrus filling.",
                    42m, "Desserts", "Vegetarian", "Milk, Egg, Wheat",
                    "Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "Chocolate Cream Puffs",
                    "Delicate choux pastries filled with cream and chocolate sauce.",
                    42m, "Desserts", "Vegetarian", "Milk, Egg, Wheat",
                    "Vegetarian, No pork, No beef, No chicken"),

                // BEVERAGES
                Dish(
                    "Lemon & Thyme Cooler",
                    "A refreshing lemon and thyme signature cooler.",
                    32m, "Beverages", "Vegetarian", "None declared",
                    "Vegan, Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "White Peach Cooler",
                    "A delicate white peach and citrus signature cooler.",
                    34m, "Beverages", "Vegetarian", "None declared",
                    "Vegan, Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "Elderflower Citrus Cooler",
                    "Elderflower and sparkling citrus with a floral finish.",
                    34m, "Beverages", "Vegetarian", "None declared",
                    "Vegan, Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "Still Mineral Water",
                    "Still mineral water, served chilled.",
                    12m, "Beverages", "Vegetarian", "None declared",
                    "Vegan, Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "Sparkling Mineral Water",
                    "Refreshing sparkling mineral water.",
                    16m, "Beverages", "Vegetarian", "None declared",
                    "Vegan, Vegetarian, No pork, No beef, No chicken")
            };


            // Images are set when a dish is first created, not overwritten later.
            var imagePaths = new Dictionary<string, string>
            {
                // SOUPS
                ["Lobster Bisque"] = "/images/menu/lobster-soup.png",
                ["Creamy Mushroom Soup"] = "/images/menu/mushroom-soup.png",
                ["French Onion Soup"] = "/images/menu/onion-soup.png",

                // APPETIZERS
                ["Burgundy-Style Garlic Butter Snails"] = "/images/menu/escargots-appertizer.png",
                ["Pan-Seared Scallops"] = "/images/menu/scallops-appertizer.png",
                ["Warm Goat Cheese Salad"] = "/images/menu/salad-appertizer.png",

                // MAIN COURSES
                ["Pan-Seared Duck Breast"] = "/images/menu/duck-main.png",
                ["Beef Fillet"] = "/images/menu/beef-main.png",
                ["Herb-Crusted Rack of Lamb"] = "/images/menu/lamb-main.png",
                ["Pan-Seared Salmon"] = "/images/menu/salmon-main.png",
                ["Roasted Chicken Breast"] = "/images/menu/chicken-main.png",
                ["Parisian-Style Gnocchi"] = "/images/menu/gnocchi-main.png",

                // DESSERTS
                ["Caramelised Vanilla Custard"] = "/images/menu/creme-brulee-dessert.png",
                ["French Lemon Tart"] = "/images/menu/lemon-tart-dessert.png",
                ["Chocolate Cream Puffs"] = "/images/menu/cream-puffs-dessert.png",

                // BEVERAGES
                ["Lemon & Thyme Cooler"] = "/images/menu/lemonade-beverage.png",
                ["White Peach Cooler"] = "/images/menu/peach-beverage.png",
                ["Elderflower Citrus Cooler"] = "/images/menu/elderflower-beverage.png",
                ["Still Mineral Water"] = "/images/menu/water-beverage.png",
                ["Sparkling Mineral Water"] = "/images/menu/sparkling-beverage.png"
            };


            // Menu items have a required MenuCategoryID foreign key. Create any
            // missing official categories before inserting new menu items.
            var categoryByName = context.MenuCategories.ToList()
                .GroupBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var categoryName in officialMenu.Select(dish => dish.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!categoryByName.ContainsKey(categoryName))
                {
                    var newCategory = new MenuCategory { Name = categoryName };
                    context.MenuCategories.Add(newCategory);
                    categoryByName.Add(categoryName, newCategory);
                }
            }

            context.SaveChanges();

            // Translate previously seeded French dish names in place when upgrading
            // an existing database. Keep MenuItemID and every other field intact, so
            // reservations, order-line relationships and billing remain unchanged.
            // Matching by exact previous name also leaves renamed/custom items alone.
            var englishNameByPreviousName = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
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
                ["Crème Brûlée"] = "Caramelised Vanilla Custard",
                ["Tarte au Citron"] = "French Lemon Tart",
                ["Profiteroles au Chocolat"] = "Chocolate Cream Puffs",
                ["Citron & Thym"] = "Lemon & Thyme Cooler",
                ["Pêche Blanche"] = "White Peach Cooler",
                ["Fleur de Sureau"] = "Elderflower Citrus Cooler",
                ["Eau Minérale"] = "Still Mineral Water",
                ["Eau Pétillante"] = "Sparkling Mineral Water",
            };
            var existingMenuItems = context.MenuItems.ToList();
            foreach (var item in existingMenuItems)
            {
                if (englishNameByPreviousName.TryGetValue(item.Name, out var englishName))
                    item.Name = englishName;
            }

            // Do not reinsert existing dishes or reset their prices, descriptions,
            // availability, category selection, images, or administrator changes.
            var existingMenuNames = existingMenuItems
                .Select(item => item.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var dish in officialMenu)
            {
                if (existingMenuNames.Add(dish.Name))
                {
                    dish.MenuCategoryID = categoryByName[dish.Category].MenuCategoryID;
                    dish.ImageUrl = imagePaths[dish.Name];
                    context.MenuItems.Add(dish);
                }
            }

            // In development only, hide known legacy sample dishes without
            // deleting them. Existing order lines and all menu records remain intact.
            if (demoAccounts)
            {
                var oldSampleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Herb-roasted chicken",
                    "Truffle mushroom risotto",
                    "Seared scallops",
                    "Burrata salad",
                    "Vanilla crème brûlée",
                    "Chocolate fondant",
                    "Iced peach tea",
                    "Sparkling water"
                };

                foreach (var item in context.MenuItems.ToList())
                {
                    if (item.CreatedByAdministratorID == null &&
                        item.UpdatedByAdministratorID == null &&
                        (oldSampleNames.Contains(item.Name) ||
                         string.Equals(item.Category, "Sides", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(item.Category, "Starters", StringComparison.OrdinalIgnoreCase)))
                    {
                        item.IsAvailable = false;
                    }
                }
            }

            context.SaveChanges();

            // Seed welcome announcement
            if (!context.Announcements.Any())
            {
                context.Announcements.Add(new Announcement
                {
                    Title = "Welcome to Céleste",
                    Message = "Reserve your table and explore our seasonal menu.",
                    CreatedAt = ReservationPolicy.Now
                });

                context.SaveChanges();
            }
        }
    }
}
