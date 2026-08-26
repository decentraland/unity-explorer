using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Puts the Game view on a **Fixed Resolution** entry matching the capture size, so what an artist
    /// frames is literally the pixels they export (see IMPLEMENTATION.md §20).
    ///
    /// Reading the current size is public API (<see cref="UnityEditor.Handles.GetMainGameViewSize"/>);
    /// *setting* it isn't — Unity has never exposed the Game view's size list, so <see cref="TryApply"/>
    /// goes through `UnityEditor.GameViewSizes` / `GameViewSize` / `GameView.selectedSizeIndex` by
    /// reflection. That recipe has been stable for many Unity versions and is verified against
    /// 6000.4.0f1, but it is still internal API: every step is null-checked and the whole thing is
    /// wrapped so a future rename degrades to a warning telling the artist to pick the size by hand,
    /// never an exception or a broken window.
    /// </summary>
    internal static class StudioGameViewSize
    {
        // Entries we add are labelled so they're recognisable in the (shared, project-wide) dropdown
        // and so we can reuse ours instead of piling up duplicates.
        private const string LABEL_PREFIX = "Outfit Studio";

        /// <summary>
        /// The main Game view's current render size in pixels — the resolution the camera actually
        /// renders at, which is what has to match the capture size for the framing to be WYSIWYG. Note
        /// this is independent of the Game view's on-screen zoom (the Scale slider), which only affects
        /// how big the already-rendered image looks in the window.
        /// </summary>
        public static Vector2Int Current
        {
            get
            {
                var s = Handles.GetMainGameViewSize();
                return new Vector2Int(Mathf.RoundToInt(s.x), Mathf.RoundToInt(s.y));
            }
        }

        public static bool Matches(int width, int height)
        {
            var c = Current;
            return c.x == width && c.y == height;
        }

        /// <summary>
        /// Add (or reuse) a Fixed Resolution entry of exactly <paramref name="width"/> ×
        /// <paramref name="height"/> in the active platform's size group and select it on every open
        /// Game view. Returns false with a human-readable <paramref name="error"/> if the internal API
        /// moved or no Game view is open.
        /// </summary>
        public static bool TryApply(int width, int height, out string error)
        {
            error = null;
            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var sizeTypeEnum = asm.GetType("UnityEditor.GameViewSizeType");
                var gameViewType = asm.GetType("UnityEditor.GameView");
                if (sizesType == null || sizeType == null || sizeTypeEnum == null || gameViewType == null)
                {
                    error = "Unity's internal Game view size API isn't where this expects it";
                    return false;
                }

                // GameViewSizes is a ScriptableSingleton<GameViewSizes>; the group is per build target,
                // so we add to whichever one the editor is currently showing.
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleton.GetProperty("instance", BindingFlags.Static | BindingFlags.Public)
                    ?.GetValue(null);
                var group = instance == null
                    ? null
                    : sizesType.GetProperty("currentGroup", BindingFlags.Instance | BindingFlags.Public)
                        ?.GetValue(instance);
                if (group == null)
                {
                    error = "couldn't reach the current Game view size group";
                    return false;
                }

                var groupType = group.GetType();
                var getTotal = groupType.GetMethod("GetTotalCount");
                var getSize = groupType.GetMethod("GetGameViewSize");
                var addCustom = groupType.GetMethod("AddCustomSize");
                var wProp = sizeType.GetProperty("width");
                var hProp = sizeType.GetProperty("height");
                var tProp = sizeType.GetProperty("sizeType");
                if (getTotal == null || getSize == null || addCustom == null ||
                    wProp == null || hProp == null || tProp == null)
                {
                    error = "Unity's internal Game view size API changed shape";
                    return false;
                }

                var fixedRes = (int)Enum.Parse(sizeTypeEnum, "FixedResolution");

                var index = -1;
                var total = (int)getTotal.Invoke(group, null);
                for (var i = 0; i < total; i++)
                {
                    var size = getSize.Invoke(group, new object[] { i });
                    if ((int)tProp.GetValue(size) != fixedRes) continue;
                    if ((int)wProp.GetValue(size) != width || (int)hProp.GetValue(size) != height) continue;
                    index = i;
                    break;
                }

                if (index < 0)
                {
                    var ctor = sizeType.GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
                    if (ctor == null)
                    {
                        error = "couldn't construct a Game view size";
                        return false;
                    }
                    var created = ctor.Invoke(new[]
                        { Enum.ToObject(sizeTypeEnum, fixedRes), width, height, $"{LABEL_PREFIX} {width}x{height}" });
                    addCustom.Invoke(group, new[] { created });
                    index = (int)getTotal.Invoke(group, null) - 1;
                }

                // Resources.FindObjectsOfTypeAll rather than GetWindow: selecting a size must not create
                // or steal focus from a Game view the artist didn't ask for.
                var views = Resources.FindObjectsOfTypeAll(gameViewType);
                if (views.Length == 0)
                {
                    error = "no Game view is open";
                    return false;
                }

                var selectedProp = gameViewType.GetProperty("selectedSizeIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (selectedProp == null)
                {
                    error = "couldn't reach GameView.selectedSizeIndex";
                    return false;
                }

                foreach (var v in views)
                {
                    selectedProp.SetValue(v, index);
                    ((EditorWindow)v).Repaint();
                }
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
