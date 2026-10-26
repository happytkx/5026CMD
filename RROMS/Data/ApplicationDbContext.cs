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
        }
    }
}
