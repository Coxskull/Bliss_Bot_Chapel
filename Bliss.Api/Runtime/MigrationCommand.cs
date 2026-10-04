namespace Bliss.Api.Runtime;

public static class MigrationCommand
{
    public const string Argument = "--migrate";

    public const string CompletionLine =
        "Migrations applied. Hosted acceptance was not claimed. Delivery remains NOT_SENT.";

    public static bool Requested(string[] args) =>
        args.Any(argument => string.Equals(argument, Argument, StringComparison.Ordinal));
}
