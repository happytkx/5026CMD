// SMTP email transport using MailKit and TLS. Sends the supplied message; it does not decide which booking qualifies for a confirmation or reminder.
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
namespace RROMS.Services;

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string body, string messageId, CancellationToken cancellationToken);
}

public class EmailService(IOptions<EmailOptions> settings) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, string messageId, CancellationToken cancellationToken)
    {
        var o = settings.Value;
        if (recipient.EndsWith(".test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Email delivery to fictional test addresses is disabled.");
        if (!o.Enabled || string.IsNullOrWhiteSpace(o.Host) || string.IsNullOrWhiteSpace(o.SenderAddress))
            throw new InvalidOperationException("Email sender is not configured.");
        var message = new MimeMessage { Subject = subject, MessageId = messageId };
        message.From.Add(new MailboxAddress(o.SenderName, o.SenderAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Body = new TextPart("plain") { Text = body };
        using var client = new SmtpClient { Timeout = 20000 };
        await client.ConnectAsync(o.Host, o.Port, o.SslOnConnect ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
        if (!string.IsNullOrWhiteSpace(o.Username))
            await client.AuthenticateAsync(o.Username, o.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
