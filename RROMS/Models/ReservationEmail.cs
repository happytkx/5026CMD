// Persistent outgoing email queue: stores confirmation/reminder content, due time, delivery state and retries so sending can happen after the booking transaction.
using System.ComponentModel.DataAnnotations;
namespace RROMS.Models;

// Durable outbox: booking transactions queue mail; SMTP runs afterward.
public class ReservationEmail
{
    public int ReservationEmailID
    {
        get; set;
    }
    public int ReservationID
    {
        get; set;
    }
    public Reservation Reservation { get; set; } = null!;
    [MaxLength(100)] public string EventKey { get; set; } = "";
    [MaxLength(100)] public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime BookingStart
    {
        get; set;
    }
    public DateTime DueAt
    {
        get; set;
    }
    public string Status { get; set; } = "Queued";
    public int Attempts
    {
        get; set;
    }
    public DateTime? DeliveredAt
    {
        get; set;
    }
    public DateTime? LeaseUntil
    {
        get; set;
    }
    public string? LastError
    {
        get; set;
    }
}
