namespace AppTemplate.Core.Interfaces;

/// <summary>Sends an email from the application's own address (<c>Mailserver:FromAddress</c>).</summary>
public interface IEmailSender
{
    Task SendAsync(
        string to,
        string from,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    );
}
