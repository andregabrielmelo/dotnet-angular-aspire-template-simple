using System.Collections.Concurrent;
using AppTemplate.Core.Interfaces;

namespace AppTemplate.FunctionalTests;

public sealed record SentEmail(string To, string From, string Subject, string Body);

public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<SentEmail> Sent { get; } = new();

    /// <summary>When set, every send throws this, like an SMTP outage.</summary>
    public Exception? FailWith { get; set; }

    public Task SendEmailAsync(
        string to,
        string from,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }
        Sent.Enqueue(new SentEmail(to, from, subject, body));
        return Task.CompletedTask;
    }
}
