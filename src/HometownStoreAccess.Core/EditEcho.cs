namespace HometownStoreAccess.Core
{
    /// <summary>Describes a change in an edit field the way a screen reader would echo typing.</summary>
    public static class EditEcho
    {
        public static string DescribeChange(string before, string after)
        {
            before = before ?? string.Empty;
            after = after ?? string.Empty;
            if (after.Length == before.Length + 1 && after.StartsWith(before, System.StringComparison.Ordinal))
            {
                return SpeakChar(after[after.Length - 1]);
            }
            if (before.Length == after.Length + 1 && before.StartsWith(after, System.StringComparison.Ordinal))
            {
                return Loc.Get("edit_deleted", SpeakChar(before[before.Length - 1]));
            }
            string t = TextCleaner.Clean(after);
            return t.Length == 0 ? Loc.Get("state_empty") : t;
        }

        public static string SpeakChar(char c)
        {
            return c == ' ' ? Loc.Get("char_space") : c.ToString();
        }
    }
}
