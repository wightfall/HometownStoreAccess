using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HometownStoreAccess.Core
{
    /// <summary>
    /// Captions the game leaves in English in every language (written into its screens or code, not
    /// in its translation tables). <see cref="TextCleaner.Clean"/> passes every text the mod reads
    /// through <see cref="Translate"/>, so these are spoken in the mod's language. Only whole texts
    /// that match exactly (or one of the patterns) change.
    /// </summary>
    public static class FixedText
    {
        private static readonly Dictionary<string, string> Exact = new Dictionary<string, string>
        {
            // Reward ("Congratulations!!") screen.
            ["You Unlock"] = "reward_new_customer",
            ["You Get"] = "reward_you_get",
            ["Popularity Up!"] = "reward_popularity_up",
            ["Trust"] = "reward_trust",
            ["Budget Up!"] = "reward_budget_up",
            ["Add Primary Need item!"] = "reward_primary_need",
            ["Add Secondary Need item!"] = "reward_secondary_need",
            ["Add Customer Skin!"] = "reward_new_look",
            // Other screens.
            ["Next Day"] = "fixed_next_day",
            ["Quit To OS"] = "fixed_quit_desktop",
            ["FullScreen"] = "fixed_fullscreen",
            ["Show Info"] = "fixed_show_info",
            ["LOAD GAME"] = "fixed_load_game",
            ["SAVE GAME"] = "fixed_save_game",
            ["No save yet"] = "fixed_no_save",
            ["File missing"] = "fixed_file_missing",
            ["Max"] = "fixed_max",
            ["None"] = "fixed_none",
            ["Are You Sure About That?"] = "fixed_are_you_sure",
            ["Master"] = "fixed_master_volume",
            ["BGM"] = "fixed_music_volume",
            ["SFX"] = "fixed_sound_volume",
            ["Save"] = "fixed_save",
            ["Return To Menu"] = "fixed_return_to_menu",
            ["Selling is disabled during the tutorial."] = "fixed_no_selling_tutorial",
            ["Enter text..."] = "fixed_save_name",
            // Credits headings (the names stay as written).
            ["Credit"] = "label_credits",
            ["Developed by"] = "fixed_developed_by",
            ["Core Team"] = "fixed_core_team",
            ["Collaborators"] = "fixed_collaborators",
            ["Testers"] = "fixed_testers",
            ["Speical Thanks"] = "fixed_special_thanks",
            ["Portrait Collection"] = "fixed_portraits",
            ["Character Collection"] = "fixed_characters",
            ["Give All"] = "fixed_give_all",
            ["Take All"] = "fixed_take_all",
            ["Size :"] = "fixed_size_label",
            ["Slot Limit"] = "fixed_slot_limit",
            ["All items"] = "fixed_all_items",
            ["Travel"] = "fixed_travel",
            ["Sell"] = "fixed_sell",
            ["All"] = "fixed_all",
            ["CheckOut"] = "fixed_checkout",
            ["Press 'Shift' to add 10"] = "fixed_shift_10",
            ["Customer Group"] = "fixed_customer_group",
            ["Customer"] = "fixed_customer",
            ["SideItem :"] = "fixed_side_item",
            ["StoreLevel Reqiure"] = "fixed_store_level_required",
            ["Event Journal"] = "fixed_event_journal",
            ["Select Skin"] = "fixed_select_skin",
            ["Confirm"] = "label_confirm",
            ["Cancel"] = "label_cancel",
            ["Daily Summary"] = "fixed_daily_summary",
            ["Total Customer:"] = "fixed_total_customers",
            ["Sastified Customer:"] = "fixed_satisfied_customers",
            ["Income"] = "fixed_income",
            ["Expense"] = "fixed_expense",
            ["Total Profit:"] = "fixed_total_profit",
            ["PAUSED"] = "fixed_paused",
            ["Master :"] = "fixed_master_volume",
            ["BGM :"] = "fixed_music_volume",
            ["SFX :"] = "fixed_sound_volume",
            ["LOAD & SAVE"] = "fixed_load_and_save",
            ["Save Name"] = "fixed_save_name",
            ["Date"] = "fixed_date",
            ["Money"] = "fixed_money",
            ["New Save Game"] = "fixed_new_save",
            ["*Warning : Save Game will Overwrite"] = "fixed_overwrite_warning",
            ["Progress will be saved up to the last completed phase."] = "fixed_progress_saved",
            ["Just want you to know"] = "fixed_just_so_you_know",
            ["Are you sure about that?"] = "fixed_are_you_sure",
            ["Your Game is Being Saved..."] = "fixed_saving",
            ["Storage"] = "label_storage_summary",
            ["*Save is enabled after completing the tutorial."] = "fixed_save_after_tutorial",
            ["You don’t have any buildings available."] = "fixed_no_buildings",
            // The main menu's own confirmation default (MainMenuControl.ShowConfirmPanel).
            ["Are You Sure About That?"] = "fixed_are_you_sure",
            // The button of the game's notice popup (NotiflyPanel), English in every language.
            ["Ok"] = "fixed_ok",
            // Credits.
            ["And to all supporters who believed in the journey."] = "fixed_credits_supporters",
        };

        private sealed class Pattern
        {
            public Regex Regex;
            public string Key;
        }

        // Amounts may have thousands separators ("1,000 baht"): the whole number is one amount.
        private static readonly Regex Baht = new Regex(@"^(-?\d{1,3}(?:,\d{3})+|-?\d+) baht$", RegexOptions.IgnoreCase);
        private static readonly Regex InnerBaht = new Regex(@"(?<![\w.,])(-?\d{1,3}(?:,\d{3})+|-?\d+) baht\b", RegexOptions.IgnoreCase);

        private static int Amount(string digits) => int.Parse(digits.Replace(",", string.Empty), CultureInfo.InvariantCulture);

        // "+18฿", "-11฿", "236 ฿": the baht sign, which some voices skip or spell out.
        private static readonly Regex BahtSign = new Regex(@"([+-]?)(\d{1,3}(?:,\d{3})+|\d+) ?\u0E3F");

        // TradePanel: $"Not Enough Storage Space for {item}", where the item prints as "Candy (Item)".
        private static readonly Regex NoSpace = new Regex(@"^Not Enough Storage Space for (.+?)(?: \((?:Item|Building|Shelf)\))?$");
        private const string NoSpaceKey = "fixed_no_storage_space";

        // Credits: "Yotsaphat Lapharotkit [Director, Game Designer, Programmer]".
        private static readonly Regex CreditRoles = new Regex(@"^(.+?) \[([A-Za-z ,\-]+)\]$");
        private static readonly Dictionary<string, string> Roles = new Dictionary<string, string>
        {
            ["Director"] = "credit_director",
            ["Game Designer"] = "credit_game_designer",
            ["Programmer"] = "credit_programmer",
            ["Assist Game Designer"] = "credit_assist_game_designer",
            ["Art Director"] = "credit_art_director",
            ["Marketing"] = "credit_marketing",
            ["Tech-Art"] = "credit_tech_art",
        };

        /// <summary>A credits line with its roles in the mod's language ("Name: role, role"); null when it is not one.</summary>
        private static string Credit(string text)
        {
            Match m = CreditRoles.Match(text);
            if (!m.Success) return null;
            var roles = new List<string>();
            foreach (string part in m.Groups[2].Value.Split(','))
            {
                if (!Roles.TryGetValue(part.Trim(), out string key)) return null;
                roles.Add(Loc.Get(key));
            }
            return m.Groups[1].Value + ": " + string.Join(", ", roles.ToArray());
        }

        /// <summary>The name shown to the player for a game asset name ("Candy"); set by the plugin.</summary>
        public static Func<string, string> AssetName = name => name;

        private static readonly Pattern[] Patterns =
        {
            new Pattern { Regex = Baht, Key = "money_baht" },
            new Pattern { Regex = new Regex(@"^Level (\d+)$"), Key = "reward_level" },
            new Pattern { Regex = new Regex(@"^Day (\d+)$"), Key = "fixed_day" },
            new Pattern { Regex = new Regex(@"^Size ?: ?(\d+)$"), Key = "fixed_size" },
            new Pattern { Regex = new Regex(@"^Storage (\d+) / Shelf (\d+)$"), Key = "fixed_storage_shelf" },
            new Pattern { Regex = new Regex(@"^Hometown Store Version: ?(.+)$"), Key = "fixed_game_version" },
            new Pattern { Regex = new Regex(@"^Version: ?(.+)$"), Key = "fixed_version" },
        };

        // The day summary names an upkeep bill "<building name>Upkeep" (SummaryPanel.AddUpkeepBill).
        private const string UpkeepSuffix = "Upkeep";
        private const string UpkeepKey = "fixed_upkeep_bill";

        /// <summary>Every mod string key used here (for tests).</summary>
        public static IEnumerable<string> Keys
        {
            get
            {
                foreach (string k in Exact.Values) yield return k;
                foreach (Pattern p in Patterns) yield return p.Key;
                yield return UpkeepKey;
                yield return NoSpaceKey;
                foreach (string k in Roles.Values) yield return k;
            }
        }

        /// <summary>The text in the mod's language when it is one of the fixed English captions; otherwise unchanged.</summary>
        public static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (Exact.TryGetValue(text, out string key)) return Loc.Get(key);
            if (text.IndexOf('\u0E3F') >= 0)
            {
                text = BahtSign.Replace(text, m => m.Groups[1].Value + Loc.Get("money_baht", Amount(m.Groups[2].Value)));
            }
            if (text[text.Length - 1] == ']')
            {
                string credit = Credit(text);
                if (credit != null) return credit;
            }
            Match noSpace = text[0] == 'N' ? NoSpace.Match(text) : Match.Empty;
            if (noSpace.Success) return Loc.Get(NoSpaceKey, AssetName(noSpace.Groups[1].Value));
            if (text.Length > UpkeepSuffix.Length && text.EndsWith(UpkeepSuffix, StringComparison.Ordinal))
            {
                return Loc.Get(UpkeepKey, text.Substring(0, text.Length - UpkeepSuffix.Length).TrimEnd());
            }
            // Amounts the game writes in English inside other text ("Upgrade: 500 baht").
            if (text.IndexOf(" baht", StringComparison.OrdinalIgnoreCase) > 0 && !Baht.IsMatch(text))
            {
                text = InnerBaht.Replace(text, m => Loc.Get("money_baht", Amount(m.Groups[1].Value)));
            }
            char first = text[0];
            // Cheap filter: every pattern starts with a digit, '-' or one of these letters.
            if (!(char.IsDigit(first) || first == '-' || first == 'L' || first == 'D' || first == 'S' || first == 'H' || first == 'V')) return text;
            foreach (Pattern p in Patterns)
            {
                Match m = p.Regex.Match(text);
                if (!m.Success) continue;
                if (p.Key == "money_baht") return Loc.Get(p.Key, Amount(m.Groups[1].Value));
                var args = new object[m.Groups.Count - 1];
                for (int i = 1; i < m.Groups.Count; i++) args[i - 1] = m.Groups[i].Value;
                return Loc.Get(p.Key, args);
            }
            return text;
        }
    }
}
