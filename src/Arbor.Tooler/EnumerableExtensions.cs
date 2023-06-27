using System.Collections.Generic;
using System.Linq;

namespace Arbor.Tooler
{
    internal static class EnumerableExtensions
    {
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> items) where T :class =>
            items.Where(item => item is { }).Cast<T>();
    }
}