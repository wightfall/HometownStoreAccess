using System.Collections.Generic;
using System.Linq;

namespace HometownStoreAccess.Core
{
    /// <summary>
    /// One message for several people at once ("3 customers came in: 2 Naughty Kid, Uncle"), instead of one
    /// message each: customers often come in and leave in small groups.
    /// </summary>
    public static class GroupedNames
    {
        /// <param name="names">The names in the order they happened, with repeats.</param>
        /// <param name="oneKey">Text for a single name: {0} = name.</param>
        /// <param name="sameKey">Text when every name is the same: {0} = name, {1} = how many.</param>
        /// <param name="mixedKey">Text for different names: {0} = how many in all, {1} = the list.</param>
        public static string Describe(IList<string> names, string oneKey, string sameKey, string mixedKey)
        {
            if (names == null || names.Count == 0) return string.Empty;
            if (names.Count == 1) return Loc.Get(oneKey, names[0]);
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (string n in names)
            {
                if (!counts.ContainsKey(n))
                {
                    counts[n] = 0;
                    order.Add(n);
                }
                counts[n]++;
            }
            if (order.Count == 1) return Loc.Get(sameKey, order[0], names.Count);
            string list = string.Join(", ", order.Select(n => counts[n] > 1 ? Loc.Get("customer_name_count", n, counts[n]) : n).ToArray());
            return Loc.Get(mixedKey, names.Count, list);
        }
    }
}
