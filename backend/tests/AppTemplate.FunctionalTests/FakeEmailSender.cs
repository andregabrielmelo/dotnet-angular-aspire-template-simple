using System.Collections.Concurrent;

namespace AppTemplate.FunctionalTests;

public sealed record SentEmail(string To, string Subject, string Body);

public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<SentEmail> Sent { get; } = new();

    /// <summary>When set, every send throws this, like an SMTP outage.</summary>
    public Exception? FailWith { get; set; }

    public Task SendAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }
        Sent.Enqueue(new SentEmail(to, subject, body));
        return Task.CompletedTask;
    }

    /// <summary>The query of the last link emailed to <paramref name="to"/> (an account email).</summary>
    public IReadOnlyDictionary<string, string> LastLinkQueryTo(string to)
    {
        var link = new Uri(
            Sent.Last(email => email.To == to)
                .Body.Split(['\n', ' '])
                .Single(word =>
                    word.StartsWith(
                        CustomWebApplicationFactory<Program>.ClientUrl,
                        StringComparison.Ordinal
                    )
                )
        );
        return Microsoft
            .AspNetCore.WebUtilities.QueryHelpers.ParseQuery(link.Query)
            .ToDictionary(part => part.Key, part => part.Value.ToString());
    }
}
