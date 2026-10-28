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

            // Seed menu
            if (!context.MenuItems.Any())
            {
                context.MenuItems.AddRange(
                    new MenuItem { Name = "Herb-roasted chicken", Description = "Roasted chicken, garden herbs and seasonal vegetables.", Price = 28.00m, Category = "Main Course", IsPopular = true, DietaryTags = "No pork", ProteinType = "Chicken", SpiceLevel = 1, Allergens = "Milk" },
                    new MenuItem { Name = "Truffle mushroom risotto", Description = "Creamy arborio rice, wild mushrooms and parmesan.", Price = 25.00m, Category = "Main Course", IsPopular = true, DietaryTags = "Vegetarian", ProteinType = "Vegetarian", Allergens = "Milk" },
                    new MenuItem { Name = "Seared scallops", Description = "Golden seared scallops with a delicate citrus butter.", Price = 22.00m, Category = "Starters", DietaryTags = "No pork, No beef, No chicken", ProteinType = "Seafood", Allergens = "Shellfish, Milk" },
                    new MenuItem { Name = "Burrata salad", Description = "Burrata, heirloom tomatoes, basil and olive oil.", Price = 18.00m, Category = "Starters", DietaryTags = "Vegetarian", ProteinType = "Vegetarian", Allergens = "Milk" },
                    new MenuItem { Name = "Vanilla crème brûlée", Description = "Classic vanilla custard with a crisp caramel top.", Price = 12.00m, Category = "Desserts", IsPopular = true, DietaryTags = "Vegetarian", ProteinType = "Vegetarian", Allergens = "Egg, Milk" },
                    new MenuItem { Name = "Chocolate fondant", Description = "Warm chocolate cake with a molten centre.", Price = 13.00m, Category = "Desserts", DietaryTags = "Vegetarian", ProteinType = "Vegetarian", Allergens = "Egg, Milk, Wheat" },
                    new MenuItem { Name = "Iced peach tea", Description = "Freshly brewed tea with peach and citrus.", Price = 7.00m, Category = "Beverages", DietaryTags = "Vegan, Vegetarian, No pork, No beef, No chicken", ProteinType = "Vegetarian", Allergens = "None declared" },
                    new MenuItem { Name = "Sparkling water", Description = "Chilled sparkling mineral water.", Price = 5.00m, Category = "Beverages", DietaryTags = "Vegan, Vegetarian, No pork, No beef, No chicken", ProteinType = "Vegetarian", Allergens = "None declared" });

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
