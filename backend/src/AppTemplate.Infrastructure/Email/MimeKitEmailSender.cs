using AppTemplate.Core.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AppTemplate.Infrastructure.Email;

public sealed class MimeKitEmailSender(
    ILoggerAdapter<MimeKitEmailSender> logger,
    IOptions<MailserverConfiguration> mailserverOptions
) : IEmailSender
{
    /// <summary>
    /// How long one SMTP operation (connect, send, quit) may take. MailKit's default is two
    /// minutes, which would hold a registration or password reset request that long on a stuck
    /// server.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly MailserverConfiguration _mailserver = mailserverOptions.Value;

    public async Task SendAsync(
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

        using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
        await client.ConnectAsync(
            _mailserver.Hostname,
            _mailserver.Port,
            SecureSocketOptions.Auto,
            cancellationToken
        );
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        // Addresses are personal data - log the subject, not who it went to or came from.
        logger.LogInformation("Sent email '{Subject}' via {Host}", subject, _mailserver.Hostname);
    }
}
