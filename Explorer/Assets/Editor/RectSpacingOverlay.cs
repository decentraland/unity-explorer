using UnityEditor;
using UnityEngine;

namespace Editor
{
    [InitializeOnLoad]
    static class RectSpacingOverlay
    {
        const EventModifiers MODIFIER = EventModifiers.Alt;

        static readonly Color ACCENT = new (1f, 0.25f, 0.45f, 0.95f);

        static GUIStyle label;
        static bool isHeld;

        static RectSpacingOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        static void OnSceneGui(SceneView view)
        {
            var e = Event.current;

            var go = Selection.activeGameObject;
            if (go == null) return;

            // Repaint/Layout events don't carry a trustworthy modifier state, so read it
            // from real input events and remember it.
            if (e.type != EventType.Repaint && e.type != EventType.Layout)
            {
                bool held = (e.modifiers & MODIFIER) != 0;

                if (held != isHeld)
                {
                    isHeld = held;
                    view.Repaint();
                }
            }

            if (!isHeld) return;

            var rt = go.GetComponent<RectTransform>();
            var parent = rt != null ? rt.parent as RectTransform : null;
            if (parent == null) return;

            label ??= new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            var pRect = parent.rect;

            var world = new Vector3[4];
            rt.GetWorldCorners(world);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var w in world)
            {
                var p = parent.InverseTransformPoint(w);
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            float midX = (minX + maxX) * 0.5f;
            float midY = (minY + maxY) * 0.5f;

            Gap(parent, new Vector2(pRect.xMin, midY), new Vector2(minX, midY));
            Gap(parent, new Vector2(maxX, midY), new Vector2(pRect.xMax, midY));
            Gap(parent, new Vector2(midX, pRect.yMin), new Vector2(midX, minY));
            Gap(parent, new Vector2(midX, maxY), new Vector2(midX, pRect.yMax));
        }

        static void Gap(RectTransform space, Vector2 a, Vector2 b)
        {
            Vector3 wa = space.TransformPoint(a);
            Vector3 wb = space.TransformPoint(b);

            Handles.color = ACCENT;
            Handles.DrawLine(wa, wb, 2f);

            if (Event.current.type != EventType.Repaint) return;

            var text = Vector2.Distance(a, b).ToString("0.#");
            Vector2 gui = HandleUtility.WorldToGUIPoint((wa + wb) * 0.5f);
            Vector2 size = label.CalcSize(new GUIContent(text));

            var box = new Rect(
                gui.x - (size.x * 0.5f) - 5f,
                gui.y - (size.y * 0.5f) - 2f,
                size.x + 10f,
                size.y + 4f);

            Handles.BeginGUI();
            EditorGUI.DrawRect(box, ACCENT);
            GUI.Label(box, text, label);
            Handles.EndGUI();
        }
    }
}
