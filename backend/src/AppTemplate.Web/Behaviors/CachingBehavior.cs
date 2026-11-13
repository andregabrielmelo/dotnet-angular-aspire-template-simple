using AppTemplate.Core.Interfaces;
using AppTemplate.Infrastructure.Caching;
using AppTemplate.UseCases.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Behaviors;

/// <summary>
/// Caches the responses of <see cref="ICacheable"/> queries in HybridCache (ADR 017), with the
/// query's key, tags and <see cref="CacheProfile"/> lifetimes, as ardalis/CleanArchitecture's
/// sample does with IMemoryCache. Unlike it, only a successful result is stored: a failure
/// (<c>NotFound</c> for an id that doesn't exist yet, say) is returned but never cached, so it
/// always reaches the handler. Concurrent misses for one key still share one handler call. Writes
/// drop entries by tag (<see cref="CacheTags"/>). Misses are logged at Debug, by key.
/// </summary>
public class CachingBehavior<TMessage, TResponse>(
    HybridCache cache,
    IOptions<CachingSettings> settings,
    IAppLogger<CachingBehavior<TMessage, TResponse>> logger
) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken
    )
    {
        if (message is not ICacheable cacheable)
        {
            return await next(message, cancellationToken);
        }

        try
        {
            return await cache.GetOrCreateAsync(
                cacheable.CacheKey,
                (message, next, logger, cacheable.CacheKey),
                static async (state, token) =>
                {
                    // The factory only runs on a miss.
                    state.logger.LogDebug("Cache miss for {CacheKey}", state.CacheKey);
                    var response = await state.next(state.message, token);
                    return response is Ardalis.Result.IResult { Status: not ResultStatus.Ok }
                        ? throw new NotCachedException(response)
                        : response;
                },
                settings.Value.EntryOptionsFor(cacheable.CacheProfile),
                cacheable.CacheTags,
                cancellationToken: cancellationToken
            );
        }
        catch (NotCachedException notCached)
        {
            // HybridCache stores nothing when the factory throws, and every caller joined to this
            // fetch gets the same exception, so they all get the same uncached response.
            return notCached.Response;
        }
    }

    // TODO: This is workaround for the fact that error results are being cached. Review if there is not a better way to handle this.
    private sealed class NotCachedException(TResponse response) : Exception
    {
        public TResponse Response { get; } = response;
    }
}
