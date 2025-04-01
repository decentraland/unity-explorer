using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Rendering.DCL_Toon
{
    /// <summary>
    /// Collection to store <see cref="DCL_Toon_MaterialHeaderScopeItem"></see>
    /// </summary>
    internal class DCL_Toon_MaterialHeaderScopeList
    {
        internal readonly uint m_DefaultExpandedState;
        internal readonly List<DCL_Toon_MaterialHeaderScopeItem> m_Items = new List<DCL_Toon_MaterialHeaderScopeItem>();

        /// <summary>
        /// Constructor that initializes it with the default expanded state for the internal scopes
        /// </summary>
        /// <param name="defaultExpandedState">By default, everything is expanded</param>
        internal DCL_Toon_MaterialHeaderScopeList(uint defaultExpandedState = uint.MaxValue)
        {
            m_DefaultExpandedState = defaultExpandedState;
        }

        /// <summary>
        /// Registers a <see cref="MaterialHeaderScopeItem"/> into the list
        /// </summary>
        /// <param name="title"><see cref="GUIContent"/> The title of the scope</param>
        /// <param name="expandable">The mask identifying the scope</param>
        /// <param name="action">The action that will be drawn if the scope is expanded</param>
        /// <param name="workflowMode">UTS workflow mode </param>        ///
        /// <param name="isTransparent">Flag transparent material header should be drawn</param>        ///
        /// <param name="isTessellation">Flag Tessellation material header should be drawn</param>        ///
        internal void RegisterHeaderScope<TEnum>(GUIContent title, TEnum expandable, Action<Material> action, uint workflowMode, uint isTransparent, uint isTessellation )
            where TEnum : struct, IConvertible
        {
            m_Items.Add(new DCL_Toon_MaterialHeaderScopeItem()
            {
                headerTitle = title,
                expandable = Convert.ToUInt32(expandable),
                drawMaterialScope = action,
                url = DCL_Toon_DocumentationUtils.GetHelpURL<TEnum>(expandable),
                workflowMode = workflowMode,
                transparentEnabled = isTransparent
            });
        }

        /// <summary>
        /// Draws all the <see cref="MaterialHeaderScopeItem"/> with its information stored
        /// </summary>
        /// <param name="materialEditor"><see cref="MaterialEditor"/></param>
        /// <param name="material"><see cref="Material"/></param>
        internal void DrawHeaders(MaterialEditor materialEditor, Material material)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));

            if (materialEditor == null)
                throw new ArgumentNullException(nameof(materialEditor));

            foreach (var item in m_Items)
            {
                using (var header = new DCL_Toon_MaterialHeaderScope(
                    item.headerTitle,
                    item.expandable,
                    materialEditor,
                    defaultExpandedState: m_DefaultExpandedState,
                    documentationURL: item.url))
                {
                    if (!header.expanded)
                        continue;

                    item.drawMaterialScope(material);

                    EditorGUILayout.Space();
                }
            }
        }
    }
}
