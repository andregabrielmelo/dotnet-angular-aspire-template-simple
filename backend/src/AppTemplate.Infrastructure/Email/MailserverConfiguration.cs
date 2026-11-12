namespace AppTemplate.Infrastructure.Email;

/// <summary>The SMTP server outgoing mail goes to (Mailpit locally), bound from <c>Mailserver</c>.</summary>
public class MailserverConfiguration
{
    public const string SectionName = "Mailserver";

    public string Hostname { get; set; } = "localhost";
    public int Port { get; set; } = 25;

    /// <summary>The sender of every email the application sends.</summary>
    public string FromAddress { get; set; } = string.Empty;
}
