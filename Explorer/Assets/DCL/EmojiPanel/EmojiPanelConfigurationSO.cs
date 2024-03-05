using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Emoji
{
    [CreateAssetMenu(fileName = "EmojiPanelConfig", menuName = "SO/EmojiPanelConfig")]
    public class EmojiPanelConfigurationSO : ScriptableObject
    {
        [SerializeField] public List<EmojiSection> EmojiSections;

    }

    [Serializable]
    public class EmojiSection
    {
        public string title;
        public string startHex;
        public string endHex;
    }
}
