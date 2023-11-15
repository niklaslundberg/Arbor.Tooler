namespace Arbor.Tooler;

internal static class StringExtensions
{
    public static string? WithDefault(this string? value, string? defaultValue) => string.IsNullOrWhiteSpace(value) ? defaultValue : value;
}