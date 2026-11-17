using System;
using System.IO;
using System.Linq;
using System.Text;
using HometownStoreAccess.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HometownStoreAccess.Diagnostics
{
    /// <summary>Developer tool: writes the full scene hierarchy (including inactive objects) to a text file.</summary>
    internal static class UiDump
    {
        public static string Write(string tag)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Hometown Store Access UI dump: " + tag + " at " + DateTime.Now.ToString("s") + " ===");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                sb.AppendLine("# Scene " + scene.name + " (loaded=" + scene.isLoaded + ")");
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    DumpObject(sb, root.transform, 0);
                }
            }
            // Objects moved to DontDestroyOnLoad.
            try
            {
                var probe = new GameObject("hsa_probe");
                UnityEngine.Object.DontDestroyOnLoad(probe);
                Scene ddol = probe.scene;
                UnityEngine.Object.DestroyImmediate(probe);
                sb.AppendLine("# Scene DontDestroyOnLoad");
                foreach (GameObject root in ddol.GetRootGameObjects())
                {
                    if (root.name == "HometownStoreAccess") continue;
                    DumpObject(sb, root.transform, 0);
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("(could not enumerate DontDestroyOnLoad: " + e.Message + ")");
            }

            string dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "hsa_dumps");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "ui_" + Sanitize(tag) + "_" + DateTime.Now.ToString("HHmmss") + ".txt");
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            Log.Info("UI dump written to " + file);
            return file;
        }

        private static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        private static void DumpObject(StringBuilder sb, Transform t, int depth)
        {
            GameObject go = t.gameObject;
            sb.Append(' ', depth * 2);
            sb.Append(go.activeSelf ? "+ " : "- ");
            sb.Append(go.name);
            sb.Append(" [");
            var comps = go.GetComponents<Component>();
            sb.Append(string.Join(",", comps.Where(c => c != null && !(c is Transform && !(c is RectTransform))).Select(c => c.GetType().Name).ToArray()));
            sb.Append("]");

            var tmp = go.GetComponent<TMP_Text>();
            if (tmp != null) sb.Append(" text=\"" + Trunc(TextCleaner.Clean(tmp.text)) + "\"");
            var legacy = go.GetComponent<Text>();
            if (legacy != null) sb.Append(" ltext=\"" + Trunc(TextCleaner.Clean(legacy.text)) + "\"");
            var sel = go.GetComponent<Selectable>();
            if (sel != null) sb.Append(" interactable=" + sel.interactable);
            var toggle = go.GetComponent<Toggle>();
            if (toggle != null) sb.Append(" isOn=" + toggle.isOn);
            var slider = go.GetComponent<Slider>();
            if (slider != null) sb.Append(" value=" + slider.value + "/" + slider.minValue + ".." + slider.maxValue);
            var image = go.GetComponent<Image>();
            if (image != null && image.sprite != null) sb.Append(" sprite=" + image.sprite.name);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null) sb.Append(" sr=" + sr.sprite.name);
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) sb.Append(" alpha=" + cg.alpha + " ia=" + cg.interactable);
            if (t is RectTransform rt)
            {
                Vector3 p = rt.position;
                sb.Append(" pos=(" + p.x.ToString("0") + "," + p.y.ToString("0") + ")");
            }
            else
            {
                Vector3 p = t.position;
                sb.Append(" wpos=(" + p.x.ToString("0.##") + "," + p.y.ToString("0.##") + ")");
            }
            sb.AppendLine();
            for (int i = 0; i < t.childCount; i++)
            {
                DumpObject(sb, t.GetChild(i), depth + 1);
            }
        }

        private static string Trunc(string s) => s.Length > 120 ? s.Substring(0, 120) + "..." : s;
    }
}
