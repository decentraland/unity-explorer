using DCL.AssetsProvision;
using DCL.ECSComponents;
using System;
using UnityEngine;

namespace DCL.Interaction.HoverCanvas
{
    [Serializable]
    public struct HoverCanvasSettings
    {
        [field: SerializeField]
        public AssetReferenceVisualTreeAsset HoverCanvasAsset { get; private set; }

        [field: SerializeField]
        public InputButtonSettings[] InputButtons { get; private set; }

        [Serializable]
        public struct InputButtonSettings
        {
            public InputAction InputAction;

            /// <summary>
            ///     TODO in the future it will be used as a localization key
            /// </summary>
            public string Key;

            public Sprite Icon;
        }
    }
}
