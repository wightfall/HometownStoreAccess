using System.Collections.Generic;
using UnityEngine;

namespace HometownStoreAccess.UI
{
    /// <summary>Caches lookups of named scene objects (including inactive ones) until the scene changes.</summary>
    internal static class SceneCache
    {
        private static readonly Dictionary<string, GameObject> ByName = new Dictionary<string, GameObject>();

        public static void Clear() => ByName.Clear();

        /// <summary>Finds a GameObject by name anywhere in the loaded scenes, active or not.</summary>
        public static GameObject Find(string name)
        {
            if (ByName.TryGetValue(name, out GameObject cached) && cached != null) return cached;
            GameObject found = null;
            foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
            {
                if (t.name == name && t.gameObject.scene.IsValid()) { found = t.gameObject; break; }
            }
            ByName[name] = found;
            return found;
        }

        /// <summary>Finds a direct or nested child by name under a parent (including inactive).</summary>
        public static GameObject FindChild(GameObject parent, string name)
        {
            if (parent == null) return null;
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t.gameObject;
            }
            return null;
        }
    }
}
