using System.Security.Claims;

namespace DocsValidator.Endpoints;

/// <summary>
/// Shared endpoint helpers to avoid repeating boilerplate across endpoint classes.
/// </summary>
internal static class EndpointHelpers
{
    /// <summary>
    /// Extracts the authenticated user's ID from the HttpContext claims.
    /// Returns <c>null</c> if the claim is missing or cannot be parsed as a Guid.
    /// </summary>
    internal static Guid? GetUserId(HttpContext httpContext)
    {
        var value = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
