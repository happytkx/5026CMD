// Applies pending database migrations and seeds sample customers, dining areas, tables and opening hours when the corresponding tables are empty.
using Microsoft.EntityFrameworkCore;
using RROMS.Models;

namespace RROMS.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
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

        }
    }
}
