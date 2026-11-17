using System.Collections.Generic;

namespace HometownStoreAccess.Core
{
    /// <summary>All translated mod strings. Keys must exist in every language (enforced by unit tests).</summary>
    internal static partial class Strings
    {
        public static readonly Dictionary<string, Dictionary<string, string>> Tables;

        // Field initializers in the per-language partial files all run before this constructor body,
        // whatever order the compiler processes the files in.
        static Strings()
        {
            Tables = new Dictionary<string, Dictionary<string, string>>
            {
                [Loc.English] = En,
                [Loc.Thai] = Th,
                [Loc.Japanese] = Ja,
                [Loc.Indonesian] = Id,
                [Loc.Chinese] = Zh,
            };
        }
    }
}
