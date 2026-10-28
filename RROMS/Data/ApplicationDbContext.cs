// Database connection for Sprint 2: maps the nine application tables and adds indexes for booking references, availability and email delivery.
using Microsoft.EntityFrameworkCore;
using RROMS.Models;

namespace RROMS.Data
{
    public class ApplicationDbContext : DbContext
    {

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers
        {
            get; set;
        }

        public DbSet<DiningArea> DiningAreas
        {
            get; set;
        }

        public DbSet<RestaurantTable> RestaurantTables
        {
            get; set;
        }

        public DbSet<Reservation> Reservations
        {
            get; set;
        }

        public DbSet<Payment> Payments
        {
            get; set;
        }

        public DbSet<ReservationHistory> ReservationHistories
        {
            get; set;
        }

        public DbSet<Notification> Notifications
        {
            get; set;
        }

        public DbSet<RestaurantSetting> RestaurantSettings
        {
            get; set;
        }

        public DbSet<ReservationEmail> ReservationEmails
        {
            get; set;
        }

        public DbSet<Bill> Bills
        {
            get; set;
        }

        public DbSet<MenuItem> MenuItems
        {
            get; set;
        }

        public DbSet<CustomerOrder> Orders
        {
            get; set;
        }

        public DbSet<OrderLine> OrderLines
        {
            get; set;
        }

        public DbSet<Review> Reviews
        {
            get; set;
        }

        public DbSet<Announcement> Announcements
        {
            get; set;
        }

        public DbSet<CustomerNotification> CustomerNotifications
        {
            get; set;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReservationEmail>().HasIndex(e => e.EventKey).IsUnique();
            modelBuilder.Entity<ReservationEmail>().HasIndex(e => new { e.Status, e.DueAt });
            modelBuilder.Entity<Reservation>().Property(r => r.BookingReference).HasMaxLength(40);
            modelBuilder.Entity<Reservation>().HasIndex(r => r.BookingReference).IsUnique();
            modelBuilder.Entity<Reservation>().HasIndex(r => new { r.TableID, r.ReservationDate, r.StartTime });

            // Each bill belongs to an existing reservation
            modelBuilder.Entity<Bill>()
                .HasOne<Reservation>()
                .WithMany()
                .HasForeignKey(b => b.ReservationID)
                .OnDelete(DeleteBehavior.Restrict);

            // Allow only one bill per reservation
            modelBuilder.Entity<Bill>()
                .HasIndex(b => b.ReservationID)
                .IsUnique();

            // Orders keep their history: customers, reservations and menu items cannot be deleted from under them
            modelBuilder.Entity<CustomerOrder>()
                .HasOne(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CustomerOrder>()
                .HasOne(o => o.Reservation).WithMany().HasForeignKey(o => o.ReservationID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CustomerOrder>().HasIndex(o => o.Reference);
            modelBuilder.Entity<CustomerOrder>().HasIndex(o => new { o.ReservationID, o.Status });
            modelBuilder.Entity<OrderLine>()
                .HasOne(l => l.Order).WithMany(o => o.Lines).HasForeignKey(l => l.OrderID);
            modelBuilder.Entity<OrderLine>()
                .HasOne(l => l.MenuItem).WithMany().HasForeignKey(l => l.MenuItemID)
                .OnDelete(DeleteBehavior.Restrict);

            // One review per customer per order
            modelBuilder.Entity<Review>()
                .HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Review>()
                .HasOne(r => r.Order).WithMany().HasForeignKey(r => r.OrderID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Review>().HasIndex(r => new { r.CustomerID, r.OrderID }).IsUnique();

            modelBuilder.Entity<CustomerNotification>()
                .HasOne(n => n.Customer).WithMany().HasForeignKey(n => n.CustomerID)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CustomerNotification>()
                .HasOne(n => n.RelatedOrder).WithMany().HasForeignKey(n => n.RelatedOrderID)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<CustomerNotification>().HasIndex(n => new { n.CustomerID, n.IsRead });
        }
    }
}
