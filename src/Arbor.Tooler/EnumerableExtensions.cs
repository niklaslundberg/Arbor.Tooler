using System.Collections.Generic;

namespace Arbor.Tooler
{
    internal static class EnumerableExtensions
    {
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> items) where T :class
        {
            foreach (var item in items)
            {
                if (item is {})
                {
                    yield return item;
                }
            }
        }
    }
}