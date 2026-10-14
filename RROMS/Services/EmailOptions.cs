// Email configuration values: sender details, SMTP connection and reminder offsets. The app password belongs in local User Secrets, never in this source file.
namespace RROMS.Services;

public class EmailOptions
{
    public bool Enabled
    {
        get; set;
    }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool SslOnConnect
    {
        get; set;
    }
    public string SenderName { get; set; } = "RROMS Restaurant";
    public string SenderAddress { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int[] ReminderHours { get; set; } = [24];
}
