using System.Collections.Generic;
using System.Text;
using HometownStoreAccess.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HometownStoreAccess.UI
{
    internal static class UiUtil
    {
        /// <summary>Active in the hierarchy and not hidden by a transparent CanvasGroup.</summary>
        public static bool IsShown(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return false;
            Transform t = go.transform;
            while (t != null)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    if (cg.alpha <= 0.01f) return false;
                    if (cg.ignoreParentGroups) break;
                }
                t = t.parent;
            }
            return true;
        }

        public static bool IsShown(Component c) => c != null && IsShown(c.gameObject);

        /// <summary>The text component directly on this object, cleaned.</summary>
        public static string OwnText(GameObject go)
        {
            if (go == null) return string.Empty;
            var tmp = go.GetComponent<TMP_Text>();
            if (tmp != null) return TextCleaner.Clean(tmp.text);
            var legacy = go.GetComponent<Text>();
            if (legacy != null) return TextCleaner.Clean(legacy.text);
            return string.Empty;
        }

        public static string Text(TMP_Text t) => t == null ? string.Empty : TextCleaner.Clean(t.text);

        /// <summary>
        /// Collects the texts under <paramref name="root"/> in hierarchy order. Input field placeholders
        /// are skipped; input fields contribute their current text instead.
        /// </summary>
        /// <param name="skipRoots">Parts of the screen to leave out (read some other way).</param>
        public static List<string> CollectTexts(GameObject root, bool includeInactive, int max = 50, ICollection<Transform> skipRoots = null)
        {
            var result = new List<string>();
            if (root == null) return result;
            var skip = new HashSet<TMP_Text>();
            foreach (var input in root.GetComponentsInChildren<TMP_InputField>(includeInactive))
            {
                if (input.placeholder is TMP_Text ph) skip.Add(ph);
            }
            Collect(root.transform, includeInactive, skip, result, max, skipRoots);
            return result;
        }

        private static void Collect(Transform t, bool includeInactive, HashSet<TMP_Text> skip, List<string> result, int max, ICollection<Transform> skipRoots)
        {
            if (result.Count >= max) return;
            if (skipRoots != null && skipRoots.Contains(t)) return;
            if (!includeInactive && !t.gameObject.activeInHierarchy) return;
            var cg = t.GetComponent<CanvasGroup>();
            if (!includeInactive && cg != null && cg.alpha <= 0.01f) return;
            var tmp = t.GetComponent<TMP_Text>();
            if (tmp != null && !skip.Contains(tmp) && (tmp.enabled || includeInactive))
            {
                string s = TextCleaner.Clean(tmp.text);
                if (!TextCleaner.IsBlank(s)) result.Add(s);
            }
            else
            {
                var legacy = t.GetComponent<Text>();
                if (legacy != null && (legacy.enabled || includeInactive))
                {
                    string s = TextCleaner.Clean(legacy.text);
                    if (!TextCleaner.IsBlank(s)) result.Add(s);
                }
            }
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), includeInactive, skip, result, max, skipRoots);
        }

        public static string JoinTexts(GameObject root, bool includeInactive, string separator = ", ", int max = 50)
        {
            return TextCleaner.Join(separator, CollectTexts(root, includeInactive, max).ToArray());
        }

        /// <summary>Best human label for a control: overrides, own texts, hover texts, parent label, name.</summary>
        public static string LabelFor(GameObject go)
        {
            if (go == null) return string.Empty;
            string over = LabelOverrides.Find(go);
            if (!string.IsNullOrEmpty(over)) return over;

            string own = LabelFromChildren(go, false);
            if (own.Length == 0) own = LabelFromChildren(go, true);
            if (own.Length == 0 && go.transform.parent != null)
            {
                own = OwnText(go.transform.parent.gameObject);
            }
            if (own.Length == 0) own = TextCleaner.HumanizeName(go.name);
            return own.TrimEnd(':', ' ', '：');
        }

        private static string LabelFromChildren(GameObject go, bool includeInactive)
        {
            var texts = CollectTexts(go, includeInactive, 4);
            return TextCleaner.Join(", ", texts.ToArray());
        }

        /// <summary>
        /// Clicks a UI element the way a mouse would: pointer enter, down, up, click, exit. This keeps the
        /// game's own click sounds, interactable checks and pointer handlers working.
        /// </summary>
        public static bool Click(GameObject go) => ClickThen(go, null);

        /// <summary>
        /// Clicks like <see cref="Click"/>, then runs <paramref name="after"/>: at once, or, when the click has
        /// to wait for its popup to finish opening, once it is made (so what the click changed is read then,
        /// not before). <paramref name="after"/> does not run when the waiting click is dropped.
        /// </summary>
        public static bool ClickThen(GameObject go, System.Action after)
        {
            if (go == null || !go.activeInHierarchy) return false;
            // A popup that is still opening (game 1.7.12) switches its buttons off for up to half a second,
            // and a click then does nothing: the click waits for the button instead.
            if (LockedByOpeningPopup(go.GetComponent<Button>()))
            {
                Log.Debug("Click on " + go.name + " waits for its popup to finish opening");
                _pendingClick = go;
                _pendingAfter = after;
                _pendingUntil = Time.unscaledTime + PendingClickTimeout;
                return true;
            }
            bool done = ClickNow(go);
            if (after != null)
            {
                try { after(); } catch (System.Exception e) { Log.Error("After click failed: " + e); }
            }
            return done;
        }

        private static bool ClickNow(GameObject go)
        {
            EventSystem es = EventSystem.current;
            if (es == null)
            {
                var button = go.GetComponent<Button>();
                if (button != null && button.interactable) { button.onClick.Invoke(); return true; }
                return false;
            }
            var data = new PointerEventData(es)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                position = ScreenPositionOf(go),
            };
            data.pointerPress = go;
            data.rawPointerPress = go;
            data.pointerEnter = go;
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerUpHandler);
            bool handled = ExecuteEvents.Execute(go, data, ExecuteEvents.pointerClickHandler);
            if (go != null && go.activeInHierarchy) ExecuteEvents.Execute(go, data, ExecuteEvents.pointerExitHandler);
            return handled;
        }

        // A click that waits for its popup to finish opening, and until when it may wait (the game's lock
        // lasts at most 0.5 s of real time).
        private const float PendingClickTimeout = 1f;
        private static GameObject _pendingClick;
        private static System.Action _pendingAfter;
        private static float _pendingUntil;

        /// <summary>True when the button is one a popup switched off while its opening animation runs.</summary>
        private static bool LockedByOpeningPopup(Button b)
        {
            if (b == null || b.enabled) return false;
            // The popup around the button first (panels the game creates while playing are not in the
            // scene list), then every popup of the scene (one can lock buttons outside its own object).
            foreach (PopupAnimation pa in b.GetComponentsInParent<PopupAnimation>(true))
            {
                if (pa != null && pa.lockedButtons != null && pa.lockedButtons.Contains(b)) return true;
            }
            foreach (PopupAnimation pa in SceneCache.All<PopupAnimation>())
            {
                if (pa != null && pa.lockedButtons != null && pa.lockedButtons.Contains(b)) return true;
            }
            return false;
        }

        /// <summary>Every frame: makes the waiting click once its popup has let go of the button.</summary>
        public static void TickPendingClick()
        {
            GameObject go = _pendingClick;
            if (go == null) return;
            if (!go.activeInHierarchy || Time.unscaledTime > _pendingUntil)
            {
                _pendingClick = null;
                _pendingAfter = null;
                return;
            }
            var b = go.GetComponent<Button>();
            if (b != null && !b.enabled) return;
            System.Action after = _pendingAfter;
            _pendingClick = null;
            _pendingAfter = null;
            ClickThen(go, after);
        }

        /// <summary>Sends only a pointer-click with the given button (used for right-click equivalents).</summary>
        public static bool ClickWith(GameObject go, PointerEventData.InputButton button)
        {
            if (go == null || !go.activeInHierarchy || EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { button = button, clickCount = 1, position = ScreenPositionOf(go) };
            return ExecuteEvents.Execute(go, data, ExecuteEvents.pointerClickHandler);
        }

        public static Vector2 ScreenPositionOf(GameObject go)
        {
            var rt = go.transform as RectTransform;
            if (rt == null) return Vector2.zero;
            var canvas = go.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(cam, rt.position);
        }

        /// <summary>Highlights the element visually (for sighted helpers) without activating input fields.</summary>
        public static void VisualFocus(GameObject go)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;
            // Never take the selection away from a text field the player is typing in.
            GameObject current = es.currentSelectedGameObject;
            if (current != null)
            {
                var editing = current.GetComponent<TMP_InputField>();
                if (editing != null && editing.isFocused) return;
            }
            if (go == null || go.GetComponent<TMP_InputField>() != null || go.GetComponent<Selectable>() == null)
            {
                if (es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<TMP_InputField>() == null)
                {
                    es.SetSelectedGameObject(null);
                }
                return;
            }
            if (es.currentSelectedGameObject != go) es.SetSelectedGameObject(go);
        }

        public static string Percent(Slider s)
        {
            float range = s.maxValue - s.minValue;
            if (range <= 0f) return s.value.ToString("0");
            if (s.wholeNumbers && range > 1f) return s.value.ToString("0") + " / " + s.maxValue.ToString("0");
            return Mathf.RoundToInt((s.value - s.minValue) / range * 100f) + "%";
        }

        public static string Describe(Transform t)
        {
            var sb = new StringBuilder();
            while (t != null)
            {
                sb.Insert(0, "/" + t.name);
                t = t.parent;
            }
            return sb.ToString();
        }
    }
}
