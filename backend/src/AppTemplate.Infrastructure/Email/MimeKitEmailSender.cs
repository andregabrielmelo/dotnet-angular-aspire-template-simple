using AppTemplate.Core.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AppTemplate.Infrastructure.Email;

public sealed partial class MimeKitEmailSender(
    ILogger<MimeKitEmailSender> logger,
    IOptions<MailserverConfiguration> mailserverOptions
) : IEmailSender
{
    private readonly MailserverConfiguration _mailserver = mailserverOptions.Value;

    public async Task SendEmailAsync(
        string to,
        string from,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _mailserver.Hostname,
            _mailserver.Port,
            SecureSocketOptions.Auto,
            cancellationToken
        );
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        // Addresses are personal data - log the subject, not who it went to.
        LogEmailSent(logger, subject, _mailserver.Hostname);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent email '{Subject}' via {Host}")]
    private static partial void LogEmailSent(ILogger logger, string subject, string host);
}
