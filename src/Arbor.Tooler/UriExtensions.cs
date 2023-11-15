using System;

namespace Arbor.Tooler;

public static class UriExtensions
{
    public static bool IsHttpOrHttps(this Uri uri)
    {
        if (uri == null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        return uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
    }
}