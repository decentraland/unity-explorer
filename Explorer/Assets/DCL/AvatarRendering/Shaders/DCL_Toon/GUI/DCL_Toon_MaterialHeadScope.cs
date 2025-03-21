﻿using System;
using UnityEngine;

namespace UnityEditor.Rendering.DCL_Toon
{
    /// <summary>
    /// Create a toggleable header for material UI, must be used within a scope.
    /// <example>Example:
    /// <code>
    /// void OnGUI()
    /// {
    ///     using (var header = new MaterialHeaderScope(text, ExpandBit, editor))
    ///     {
    ///         if (header.expanded)
    ///             EditorGUILayout.LabelField("Hello World !");
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </summary>
    internal struct DCL_Toon_MaterialHeaderScope : IDisposable
    {
        /// <summary>Indicates whether the header is expanded or not. Is true if the header is expanded, false otherwise.</summary>
        internal readonly bool expanded;
        bool spaceAtEnd;

        internal static void DrawSplitter(bool isBoxed = false)
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f);
            float xMin = rect.xMin;

            // Splitter rect should be full-width
            rect.xMin = 0f;
            rect.width += 4f;

            if (isBoxed)
            {
                rect.xMin = xMin == 7.0 ? 4.0f : EditorGUIUtility.singleLineHeight;
                rect.width -= 1;
            }

            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, !EditorGUIUtility.isProSkin
                ? new Color(0.6f, 0.6f, 0.6f, 1.333f)
                : new Color(0.12f, 0.12f, 0.12f, 1.333f));
        }
        /// <summary> Draw a foldout header </summary>
        /// <param name="title"> The title of the header </param>
        /// <param name="state"> The state of the header </param>
        /// <returns>return the state of the foldout header</returns>
        internal static bool DrawHeaderFoldout(GUIContent title, bool state, bool isBoxed = false, Func<bool> hasMoreOptions = null, Action toggleMoreOptions = null, string documentationURL = "", Action<Vector2> contextAction = null)
        {
            return EditorGUILayout.Foldout(state, title);
        }
        internal static bool DrawSubHeaderFoldout(GUIContent title, bool state, bool isBoxed = false)
        {
            return EditorGUILayout.Foldout(state, title);
        }

        /// <summary>
        /// Creates a material header scope to display the foldout in the material UI.
        /// </summary>
        /// <param name="title">GUI Content of the header.</param>
        /// <param name="bitExpanded">Bit index which specifies the state of the header (whether it is open or collapsed) inside Editor Prefs.</param>
        /// <param name="materialEditor">The current material editor.</param>
        /// <param name="spaceAtEnd">Set this to true to make the block include space at the bottom of its UI. Set to false to not include any space.</param>
        /// <param name="subHeader">Set to true to make this into a sub-header. This affects the style of the header. Set to false to make this use the standard style.</param>
        /// <param name="defaultExpandedState">The default state if the header is not present</param>
        /// <param name="documentationURL">[optional] Documentation page</param>
        internal DCL_Toon_MaterialHeaderScope(GUIContent title, uint bitExpanded, MaterialEditor materialEditor, bool spaceAtEnd = true, bool subHeader = false, uint defaultExpandedState = uint.MaxValue, string documentationURL = "")
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));

            bool beforeExpanded = materialEditor.IsAreaExpanded(bitExpanded, defaultExpandedState);

            this.spaceAtEnd = spaceAtEnd;
            if (!subHeader)
                DrawSplitter();
            GUILayout.BeginVertical();

            bool saveChangeState = GUI.changed;
            expanded = subHeader
                ? DrawSubHeaderFoldout(title, beforeExpanded, isBoxed: false)
                : DrawHeaderFoldout(title, beforeExpanded, documentationURL: documentationURL);
            if (expanded ^ beforeExpanded)
            {
                materialEditor.SetIsAreaExpanded((uint)bitExpanded, expanded);
                saveChangeState = true;
            }
            GUI.changed = saveChangeState;

            if (expanded)
                ++EditorGUI.indentLevel;
        }

        /// <summary>
        /// Creates a material header scope to display the foldout in the material UI.
        /// </summary>
        /// <param name="title">Title of the header.</param>
        /// <param name="bitExpanded">Bit index which specifies the state of the header (whether it is open or collapsed) inside Editor Prefs.</param>
        /// <param name="materialEditor">The current material editor.</param>
        /// <param name="spaceAtEnd">Set this to true to make the block include space at the bottom of its UI. Set to false to not include any space.</param>
        /// <param name="subHeader">Set to true to make this into a sub-header. This affects the style of the header. Set to false to make this use the standard style.</param>
        internal DCL_Toon_MaterialHeaderScope(string title, uint bitExpanded, MaterialEditor materialEditor, bool spaceAtEnd = true, bool subHeader = false)
            : this(EditorGUIUtility.TrTextContent(title, string.Empty), bitExpanded, materialEditor, spaceAtEnd, subHeader)
        {
        }

        /// <summary>Disposes of the material scope header and cleans up any resources it used.</summary>
        void IDisposable.Dispose()
        {
            if (expanded)
            {
                if (spaceAtEnd && (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout))
                    EditorGUILayout.Space();
                --EditorGUI.indentLevel;
            }

            GUILayout.EndVertical();
        }
    }

}
