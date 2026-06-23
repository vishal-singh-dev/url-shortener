namespace Api.Services;

public enum RedirectLookupStatus
{
    Found,
    NotFound,
    Expired
}

public sealed record RedirectLookupResult(
    RedirectLookupStatus Status,
    string? LongUrl);
