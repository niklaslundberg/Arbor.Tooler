namespace Arbor.Tooler
{
    internal static class StringExtensions
    {
        public static string WithDefault(this string value, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return value;
        }
    }
}
