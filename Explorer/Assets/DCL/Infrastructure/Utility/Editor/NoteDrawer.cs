using UnityEditor;
using UnityEngine;

namespace Utility.Editor
{
    [CustomPropertyDrawer(typeof(NoteAttribute))]
    public class NoteDrawer : DecoratorDrawer
    {
        private const float PADDING_TOP = 4f;
        private const float PADDING_BOTTOM = 2f;

        private float cachedHeight;

        public override float GetHeight()
        {
            if (cachedHeight > 0f)
                return cachedHeight;

            // Fallback estimate when called outside OnGUI (UI Toolkit binding path).
            float lineCount = 1 + ((NoteAttribute)attribute).Text.Length / 60;
            return lineCount * EditorGUIUtility.singleLineHeight + PADDING_TOP + PADDING_BOTTOM;
        }

        public override void OnGUI(Rect position)
        {
            var note = (NoteAttribute)attribute;

            // Recalculate and cache the real height now that we're inside OnGUI.
            float boxHeight = EditorStyles.helpBox.CalcHeight(new GUIContent(note.Text), position.width);
            cachedHeight = Mathf.Max(boxHeight, EditorGUIUtility.singleLineHeight) + PADDING_TOP + PADDING_BOTTOM;

            position.y += PADDING_TOP;
            position.height -= PADDING_TOP + PADDING_BOTTOM;
            EditorGUI.HelpBox(position, note.Text, MessageType.Info);
        }
    }
}
