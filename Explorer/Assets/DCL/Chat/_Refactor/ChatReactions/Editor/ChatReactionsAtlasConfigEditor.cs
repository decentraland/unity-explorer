#if UNITY_EDITOR
using DCL.Chat.ChatReactions.Configs;
using UnityEditor;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Editor
{
    [CustomEditor(typeof(ChatReactionsAtlasConfig))]
    public class ChatReactionsAtlasConfigEditor : UnityEditor.Editor
    {
        private bool showMappings;
        private Vector2 scrollPos;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (ChatReactionsAtlasConfig)target;

            EditorGUILayout.Space(8);

            if (config.SpriteAsset == null)
            {
                EditorGUILayout.HelpBox("Assign a TMP_SpriteAsset to see Unicode → Tile mappings.", MessageType.Info);
                return;
            }

            var chars = config.SpriteAsset.spriteCharacterTable;

            showMappings = EditorGUILayout.Foldout(showMappings, $"Unicode → Tile Mappings ({chars.Count} entries)", true);

            if (!showMappings) return;

            EditorGUI.indentLevel++;

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(300));

            for (int i = 0; i < chars.Count; i++)
            {
                uint unicode = chars[i].unicode;
                int glyphIdx = (int)chars[i].glyphIndex;

                string emoji;

                if (unicode >= 0xD800 && unicode <= 0xDFFF || unicode > 0x10FFFF)
                    emoji = "?";
                else if (unicode <= 0xFFFF)
                    emoji = ((char)unicode).ToString();
                else
                    emoji = char.ConvertFromUtf32((int)unicode);

                EditorGUILayout.LabelField($"[{i}]  {emoji}  U+{unicode:X4}", $"tile {glyphIdx}");
            }

            EditorGUILayout.EndScrollView();

            EditorGUI.indentLevel--;
        }
    }
}
#endif
