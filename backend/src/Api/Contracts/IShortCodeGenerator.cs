namespace Api.Contracts;

public interface IShortCodeGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken);
}
