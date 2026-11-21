using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HometownStoreAccess.UI
{
    /// <summary>
    /// Caches lookups of scene objects (including inactive ones) until the scene changes. Searching the
    /// whole scene is slow, and screens check whether they are open every frame, so a lookup that finds
    /// nothing is only repeated after a short wait instead of on every frame.
    /// </summary>
    internal static class SceneCache
    {
        // How long a lookup that found nothing is trusted before the scene is searched again. A search
        // of the game scene takes 20-50 ms, so a miss must not be repeated often (it would stutter).
        private const float NameMissRetry = 3f;
        private const float TypeMissRetry = 1f;
        // An empty list (scene not ready yet) is gathered again after this; a full one is kept until the
        // next scene loads (the panels it is used for are part of the scene, never created later).
        private const float EmptyListRetry = 2f;

        private static readonly Dictionary<string, GameObject> ByName = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, float> NameMissUntil = new Dictionary<string, float>();
        private static readonly Dictionary<Type, Object> ByType = new Dictionary<Type, Object>();
        private static readonly Dictionary<Type, float> TypeMissUntil = new Dictionary<Type, float>();
        private static readonly Dictionary<Type, Array> Lists = new Dictionary<Type, Array>();
        private static readonly Dictionary<Type, float> ListUntil = new Dictionary<Type, float>();

        /// <summary>Number of full scene searches since the scene loaded (for the perf command).</summary>
        public static int Searches { get; private set; }

        public static void Clear()
        {
            ByName.Clear();
            NameMissUntil.Clear();
            ByType.Clear();
            TypeMissUntil.Clear();
            Lists.Clear();
            ListUntil.Clear();
            Searches = 0;
        }

        /// <summary>Finds a GameObject by name anywhere in the loaded scenes, active or not.</summary>
        public static GameObject Find(string name)
        {
            if (ByName.TryGetValue(name, out GameObject cached) && cached != null) return cached;
            float now = Time.unscaledTime;
            if (NameMissUntil.TryGetValue(name, out float until) && now < until) return null;
            Searches++;
            GameObject found = null;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == name && t.gameObject.scene.IsValid()) { found = t.gameObject; break; }
            }
            ByName[name] = found;
            if (found == null) NameMissUntil[name] = now + NameMissRetry;
            else NameMissUntil.Remove(name);
            return found;
        }

        /// <summary>The scene's component (or object) of type <typeparamref name="T"/>, active or not.</summary>
        public static T Get<T>() where T : Object
        {
            Type type = typeof(T);
            if (ByType.TryGetValue(type, out Object cached) && cached != null) return (T)cached;
            float now = Time.unscaledTime;
            if (TypeMissUntil.TryGetValue(type, out float until) && now < until) return null;
            Searches++;
            T found = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            ByType[type] = found;
            if (found == null) TypeMissUntil[type] = now + TypeMissRetry;
            else TypeMissUntil.Remove(type);
            return found;
        }

        /// <summary>
        /// Every component of type <typeparamref name="T"/> in the loaded scenes, active or not. Meant for
        /// objects that are part of the scene (not created while playing).
        /// </summary>
        public static T[] All<T>() where T : Component
        {
            Type type = typeof(T);
            float now = Time.unscaledTime;
            if (Lists.TryGetValue(type, out Array cached))
            {
                var list = (T[])cached;
                bool intact = list.Length > 0 || (ListUntil.TryGetValue(type, out float until) && now < until);
                foreach (T c in list)
                {
                    if (c == null) { intact = false; break; }
                }
                if (intact) return list;
            }
            Searches++;
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Lists[type] = found;
            ListUntil[type] = now + EmptyListRetry;
            return found;
        }

        /// <summary>A scene was added to the loaded ones: lists of components are gathered again.</summary>
        public static void SceneAdded()
        {
            Lists.Clear();
            ListUntil.Clear();
            TypeMissUntil.Clear();
            NameMissUntil.Clear();
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
