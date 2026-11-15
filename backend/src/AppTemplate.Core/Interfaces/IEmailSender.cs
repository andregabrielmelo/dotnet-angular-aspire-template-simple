namespace AppTemplate.Core.Interfaces;

/// <summary>
/// Provides an abstraction for sending email messages.
/// </summary>
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
