﻿using System;
using UnityEngine;

namespace UnityEditor.Rendering.DCL_Toon
{
    internal struct DCL_Toon_MaterialHeaderScopeItem
    {
        /// <summary><see cref="GUIContent"></see> that will be rendered on the <see cref="MaterialHeaderScope"></see></summary>
        public GUIContent headerTitle { get; set; }
        /// <summary>The bitmask for this scope</summary>
        public uint expandable { get; set; }
        /// <summary>The action that will draw the controls for this scope</summary>
        public Action<Material> drawMaterialScope { get; set; }
        /// <summary>The url of the scope</summary>
        public string url { get; set; }
        /// <summary>The mode of UTS rendering mode</summary>
        public uint workflowMode { get; set; }
        /// <summary>The flag wheter UTS material is in transparent mode</summary>
        public uint transparentEnabled { get; set; }
    }
}
