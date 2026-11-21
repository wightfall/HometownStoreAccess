using System.Collections.Generic;
using UnityEngine;

namespace HometownStoreAccess.UI
{
    /// <summary>
    /// A screen, panel or mode the player can be in (main menu, shelf panel, map, shop floor...).
    /// The active context with the highest priority receives input and builds the navigation list.
    /// </summary>
    internal abstract class ScreenContext
    {
        public abstract string Id { get; }

        /// <summary>Higher wins when several contexts are active (popups above panels above the HUD).</summary>
        public virtual int Priority => 0;

        public abstract bool IsActive();

        /// <summary>Spoken when the context becomes active (panel name).</summary>
        public virtual string Title() => null;

        /// <summary>Extra text spoken after the title on entry (message body, current line...).</summary>
        public virtual string Intro() => null;

        /// <summary>Fills the navigation list. Called on entry and regularly while active.</summary>
        public abstract void Build(List<NavItem> items);

        /// <summary>Context-specific keys, checked before the default list navigation. Return true to consume.</summary>
        public virtual bool HandleInput() => false;

        public virtual void OnEnter() { }
        public virtual void OnExit() { }

        /// <summary>Per-frame hook while active (watch for changes worth announcing).</summary>
        public virtual void Tick() { }

        /// <summary>
        /// Screens dismissed by pressing Enter repeatedly (dialog, rewards, popups) set this so that an
        /// extra Enter right after they close does not also activate something on the screen below.
        /// </summary>
        public virtual bool GuardsEnterOnExit => false;

        /// <summary>History category of the title and intro spoken on entry.</summary>
        public virtual HometownStoreAccess.Core.MsgCategory IntroCategory => HometownStoreAccess.Core.MsgCategory.All;

        /// <summary>Whether the focused item is read after the title when the screen opens.</summary>
        public virtual bool AnnounceFocusOnEnter => true;

        /// <summary>When true, Space is left to the game (dialog advance) instead of activating items.</summary>
        public virtual bool ReservesSpace => false;

        /// <summary>When false, the context handles all arrow keys itself (shop floor, placement).</summary>
        public virtual bool UsesListNavigation => true;

        /// <summary>Return to the first item each time the context is entered instead of the last focus.</summary>
        public virtual bool ResetFocusOnEnter => false;

        /// <summary>
        /// Where focus goes when the focused item disappears (a sub-list opened or closed), when that item
        /// is in the new list; null keeps the default (the same position).
        /// </summary>
        public virtual string PreferredFocusKey() => null;

        /// <summary>
        /// Where focus starts when the screen opens and no earlier focus is remembered; -1 (or 0) is the
        /// first item.
        /// </summary>
        public virtual int InitialFocus(IReadOnlyList<NavItem> items) => -1;

        /// <summary>Help lines specific to this screen, appended to the general help.</summary>
        public virtual IEnumerable<string> HelpLines() { yield break; }

        /// <summary>Everything readable on this screen, for "read all".</summary>
        public virtual string ReadAll() => null;

        /// <summary>Root object used for visual focus checks and read-all fallback.</summary>
        public virtual GameObject Root => null;
    }
}
