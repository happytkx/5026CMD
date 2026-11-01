// Applies pending database migrations and seeds sample customers, dining areas, tables, opening hours, menu and announcements when the corresponding tables are empty.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RROMS.Models;
using RROMS.Services;

namespace RROMS.Data
{
    public static class DbInitializer
    {
        // Development staff/administrator sign-ins; created only when demoAccounts is true and the email is not yet registered.
        private static readonly (string Name, string Email, string Password, string Role)[] DemoAccounts =
        [
            ("Celeste Administrator", "admin@celeste.local", "Admin123!", Customer.AdminRole),
            ("Restaurant Staff", "staff@celeste.local", "Staff123!", Customer.StaffRole)
        ];

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
                var hasher = new PasswordHasher<Customer>();

                foreach (var (name, email, password, role) in DemoAccounts)
                {
                    if (context.Customers.Any(c => c.Email == email))
                        continue;

                    var account = new Customer
                    {
                        CustomerName = name,
                        Email = email,
                        Role = role,
                        CreatedAt = ReservationPolicy.Now
                    };
                    account.PasswordHash = hasher.HashPassword(account, password);
                    context.Customers.Add(account);
                }

                context.SaveChanges();
            }

            // Seed the catalogue only once. Never overwrite administrator changes
            // to names, descriptions, prices, categories, images or availability.
            if (!context.MenuItems.Any())
            {
            // Seed the official Céleste menu.
            // Previous sample dishes are hidden, not deleted,
            // to preserve historical orders and billing records.

            var oldSampleNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
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


            // Hide old sample dishes and all side dishes.
            foreach (var item in context.MenuItems.ToList())
            {
                if (oldSampleNames.Contains(item.Name) ||
                    string.Equals(
                        item.Category,
                        "Sides",
                        StringComparison.OrdinalIgnoreCase))
                {
                    item.IsAvailable = false;
                }
            }


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
                    "Bisque de Homard",
                    "Velvety lobster bisque with crème fraîche and chive oil.",
                    58m, "Soups", "Seafood", "Shellfish, Milk",
                    "No pork, No beef, No chicken", true),

                Dish(
                    "Velouté de Champignons",
                    "Silky wild mushroom soup finished with truffle oil.",
                    48m, "Soups", "Vegetarian", "Milk",
                    "Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "Soupe à l'Oignon",
                    "Slow-cooked onion soup with vegetable stock and Gruyère toast.",
                    46m, "Soups", "Vegetarian", "Milk, Wheat",
                    "Vegetarian, No pork, No beef, No chicken"),

                // APPETIZERS
                Dish(
                    "Escargots de Bourgogne",
                    "Burgundy-style escargots in garlic and parsley butter.",
                    52m, "Appetizers", "Molluscs", "Milk, Molluscs"),

                Dish(
                    "Saint-Jacques",
                    "Seared scallops with cauliflower purée and citrus butter.",
                    72m, "Appetizers", "Seafood", "Shellfish, Milk",
                    "No pork, No beef, No chicken", true),

                Dish(
                    "Salade de Chèvre Chaud",
                    "Warm goat cheese salad with baby leaves, walnuts and honey.",
                    46m, "Appetizers", "Vegetarian", "Milk, Tree nuts",
                    "Vegetarian, No pork, No beef, No chicken"),

                // MAIN COURSES
                Dish(
                    "Magret de Canard",
                    "Pan-seared duck breast with seasonal vegetables and a rich jus.",
                    138m, "Main Course", "Duck", "Milk",
                    "No pork", true),

                Dish(
                    "Filet de Bœuf",
                    "Tender beef fillet with potato purée and peppercorn sauce.",
                    178m, "Main Course", "Beef", "Milk",
                    "No pork", true),

                Dish(
                    "Carré d'Agneau",
                    "Herb-crusted lamb rack with roasted vegetables and rosemary jus.",
                    188m, "Main Course", "Lamb", "Wheat, Milk",
                    "No pork"),

                Dish(
                    "Saumon Poêlé",
                    "Pan-seared salmon with seasonal greens and lemon butter sauce.",
                    118m, "Main Course", "Seafood", "Fish, Milk",
                    "No pork, No beef, No chicken"),

                Dish(
                    "Suprême de Poulet",
                    "Roasted chicken supreme with garden herbs and potato gratin.",
                    98m, "Main Course", "Chicken", "Milk",
                    "No pork"),

                Dish(
                    "Gnocchis Parisiens",
                    "Parisian-style gnocchi with wild mushrooms and creamy Parmesan sauce.",
                    88m, "Main Course", "Vegetarian", "Milk, Egg, Wheat",
                    "Vegetarian, No pork, No beef, No chicken"),

                // DESSERTS
                Dish(
                    "Crème Brûlée",
                    "Classic vanilla custard beneath a crisp caramelised sugar crust, served with fresh berries.",
                    38m, "Desserts", "Vegetarian", "Milk, Egg",
                    "Vegetarian, No pork, No beef, No chicken", true),

                Dish(
                    "Tarte au Citron",
                    "French lemon tart with buttery pastry and delicate citrus filling.",
                    42m, "Desserts", "Vegetarian", "Milk, Egg, Wheat",
                    "Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "Profiteroles au Chocolat",
                    "Delicate choux pastries filled with cream and chocolate sauce.",
                    42m, "Desserts", "Vegetarian", "Milk, Egg, Wheat",
                    "Vegetarian, No pork, No beef, No chicken"),

                // BEVERAGES
                Dish(
                    "Citron & Thym",
                    "A refreshing lemon and thyme signature cooler.",
                    32m, "Beverages", "Vegetarian", "None declared",
                    "Vegan, Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "Pêche Blanche",
                    "A delicate white peach and citrus signature cooler.",
                    34m, "Beverages", "Vegetarian", "None declared",
                    "Vegan, Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "Fleur de Sureau",
                    "Elderflower and sparkling citrus with a floral finish.",
                    34m, "Beverages", "Vegetarian", "None declared",
                    "Vegan, Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "Eau Minérale",
                    "Still mineral water, served chilled.",
                    12m, "Beverages", "Vegetarian", "None declared",
                    "Vegan, Vegetarian, No pork, No beef, No chicken"),

                Dish(
                    "Eau Pétillante",
                    "Refreshing sparkling mineral water.",
                    16m, "Beverages", "Vegetarian", "None declared",
                    "Vegan, Vegetarian, No pork, No beef, No chicken")
            };

            // Check existing names to prevent duplicate dishes.
            var existingMenuNames = context.MenuItems
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Add only dishes that do not already exist.
            foreach (var dish in officialMenu)
                {
                    if (!existingMenuNames.Contains(dish.Name))
                    {
                        context.MenuItems.Add(dish);
                        existingMenuNames.Add(dish.Name);
                    }
                }


            // Save the changes.
            context.SaveChanges();


            // Synchronise existing official menu information.
            var savedMenuItems = context.MenuItems.ToList();

            foreach (var dish in officialMenu)
            {
                var existing = savedMenuItems.FirstOrDefault(
                    item => string.Equals(
                        item.Name,
                        dish.Name,
                        StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.Description = dish.Description;
                    existing.Allergens = dish.Allergens;
                    existing.ProteinType = dish.ProteinType;
                }
            }

            context.SaveChanges();

            // Assign images to the official Céleste menu.
            var imagePaths = new Dictionary<string, string>
            {
                // SOUPS
                ["Bisque de Homard"] = "/images/menu/lobster-soup.png",
                ["Velouté de Champignons"] = "/images/menu/mushroom-soup.png",
                ["Soupe à l'Oignon"] = "/images/menu/onion-soup.png",

                // APPETIZERS
                ["Escargots de Bourgogne"] = "/images/menu/escargots-appertizer.png",
                ["Saint-Jacques"] = "/images/menu/scallops-appertizer.png",
                ["Salade de Chèvre Chaud"] = "/images/menu/salad-appertizer.png",

                // MAIN COURSES
                ["Magret de Canard"] = "/images/menu/duck-main.png",
                ["Filet de Bœuf"] = "/images/menu/beef-main.png",
                ["Carré d'Agneau"] = "/images/menu/lamb-main.png",
                ["Saumon Poêlé"] = "/images/menu/salmon-main.png",
                ["Suprême de Poulet"] = "/images/menu/chicken-main.png",
                ["Gnocchis Parisiens"] = "/images/menu/gnocchi-main.png",

                // DESSERTS
                ["Crème Brûlée"] = "/images/menu/creme-brulee-dessert.png",
                ["Tarte au Citron"] = "/images/menu/lemon-tart-dessert.png",
                ["Profiteroles au Chocolat"] = "/images/menu/cream-puffs-dessert.png",

                // BEVERAGES
                ["Citron & Thym"] = "/images/menu/lemonade-beverage.png",
                ["Pêche Blanche"] = "/images/menu/peach-beverage.png",
                ["Fleur de Sureau"] = "/images/menu/elderflower-beverage.png",
                ["Eau Minérale"] = "/images/menu/water-beverage.png",
                ["Eau Pétillante"] = "/images/menu/sparkling-beverage.png"
            };

            // Update image paths for existing and newly added dishes.
            foreach (var item in context.MenuItems.ToList())
            {
                if (imagePaths.TryGetValue(item.Name, out var imagePath))
                {
                    item.ImageUrl = imagePath;
                }
            }

            context.SaveChanges();

            }

            // Fresh databases receive menu categories after the catalogue is seeded.
            // Migrated databases are backfilled by the AddMenuCategories migration.
            if (!context.MenuCategories.Any())
            {
                var categories = context.MenuItems.Select(i => i.Category).Distinct().ToList();
                context.MenuCategories.AddRange(categories.Select(name => new MenuCategory { Name = name }));
                context.SaveChanges();
            }

            // Remove obsolete, unreferenced legacy demo categories from local
            // development data. Historical order lines always take priority:
            // referenced dishes are retained and merely made unavailable.
            // Production and non-demo databases are never cleaned automatically.
            if (demoAccounts)
            {
                var obsoleteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Sides", "Starters"
                };
                foreach (var item in context.MenuItems.ToList()
                    .Where(i => obsoleteNames.Contains(i.Category)))
                {
                    if (context.OrderLines.Any(l => l.MenuItemID == item.MenuItemID))
                        item.IsAvailable = false;
                    else
                        context.MenuItems.Remove(item);
                }
                context.SaveChanges();

                foreach (var category in context.MenuCategories.ToList()
                    .Where(c => obsoleteNames.Contains(c.Name)))
                {
                    if (!context.MenuItems.Any(i => i.Category == category.Name))
                        context.MenuCategories.Remove(category);
                }
                context.SaveChanges();
            }

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
