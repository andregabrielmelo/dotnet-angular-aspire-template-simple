using System.Globalization;
using AppTemplate.UseCases.Constants;
using Microsoft.AspNetCore.WebUtilities;

namespace AppTemplate.Web.Extensions;

public static class HttpContextExtensions
{
    public static void AddPaginationLinks(
        this HttpContext context,
        int page,
        int perPage,
        int totalPages
    )
    {
        var request = context.Request;
        var baseUrl = request.PathBase.Add(request.Path).ToUriComponent();

        // Preserve existing query parameters (filters, sorting, search, etc.).
        var query = request
            .Query.SelectMany(
                pair => pair.Value,
                (pair, value) => new KeyValuePair<string, string?>(pair.Key, value)
            )
            .Where(pair =>
                !string.Equals(
                    pair.Key,
                    PaginationConstants.PAGE_PARAMETER,
                    StringComparison.OrdinalIgnoreCase
                )
                && !string.Equals(
                    pair.Key,
                    PaginationConstants.PAGE_SIZE_PARAMETER,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToList();

        string Link(string rel, int targetPage)
        {
            var parameters = new List<KeyValuePair<string, string?>>(query)
            {
                new(
                    PaginationConstants.PAGE_PARAMETER,
                    targetPage.ToString(CultureInfo.InvariantCulture)
                ),
                new(
                    PaginationConstants.PAGE_SIZE_PARAMETER,
                    perPage.ToString(CultureInfo.InvariantCulture)
                ),
            };

            var url = QueryHelpers.AddQueryString(baseUrl, parameters);

            return $"<{url}>; rel=\"{rel}\"";
        }

        var links = new List<string>();

        if (page > 1)
        {
            links.Add(Link("first", 1));
            links.Add(Link("prev", page - 1));
        }

        if (page < totalPages)
        {
            links.Add(Link("next", page + 1));
            links.Add(Link("last", totalPages));
        }

        if (links.Count > 0)
        {
            context.Response.Headers.Link = string.Join(", ", links);
        }
    }
}
