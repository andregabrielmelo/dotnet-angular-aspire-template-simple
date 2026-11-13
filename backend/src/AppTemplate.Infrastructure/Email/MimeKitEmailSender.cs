using AppTemplate.Core.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AppTemplate.Infrastructure.Email;

public sealed class MimeKitEmailSender(
    ILogger<MimeKitEmailSender> logger,
    IOptions<MailserverConfiguration> mailserverOptions
) : IEmailSender
{
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

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _mailserver.Hostname,
            _mailserver.Port,
            SecureSocketOptions.Auto,
            cancellationToken
        );
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogWarning("Sending email to {to} from {from} with subject {subject} using {type}.", to, from, subject, this.ToString());
    }
}
