namespace Api.Models;

public sealed record ClickEvent(
    string Code,
    DateTimeOffset OccurredAtUtc,
    string? Referrer,
    string? UserAgent);
