using System.Collections.Concurrent;
using System.Net;
using System.Text;
using AppTemplate.ServiceDefaults.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AppTemplate.FunctionalTests.Logging;

/// <summary>
/// Captures what a host's two log sinks write: the console's formatted text, and the OTLP
/// sink's HTTP/protobuf export requests as raw bytes. Assertions run on that final output, so
/// they hold whatever happens between the logger call and the wire.
/// </summary>
public sealed class LogCapture : HttpMessageHandler
{
    public const string OtlpEndpoint = "http://otlp.test";

    private readonly StringWriter _console = new();
    private readonly ConcurrentQueue<byte[]> _exports = new();

    public void Configure(IServiceCollection services) =>
        services.Configure<LoggingOutputOptions>(options =>
        {
            options.ConsoleWriter = TextWriter.Synchronized(_console);
            options.OtlpEndpoint = OtlpEndpoint;
            options.OtlpHandler = this;
        });

    public string Console => _console.ToString();

    /// <summary>All OTLP export bodies so far, decoded as UTF-8 (protobuf keeps strings verbatim).</summary>
    public string Otlp => string.Concat(_exports.Select(Encoding.UTF8.GetString));

    /// <summary>Waits until both sinks have written something containing <paramref name="marker"/>.</summary>
    public async Task WaitForAsync(string marker, int occurrences = 1)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (Count(Console, marker) < occurrences || Count(Otlp, marker) < occurrences)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException(
                    $"'{marker}' not logged {occurrences} times. Console:\n{Console}"
                );
            }
            await Task.Delay(50);
        }
    }

    private static int Count(string text, string marker)
    {
        var count = 0;
        for (
            var i = text.IndexOf(marker, StringComparison.Ordinal);
            i >= 0;
            i = text.IndexOf(marker, i + 1, StringComparison.Ordinal)
        )
        {
            count++;
        }
        return count;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        _exports.Enqueue(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
        // An empty ExportLogsServiceResponse is a valid, empty protobuf message.
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
    }
}
