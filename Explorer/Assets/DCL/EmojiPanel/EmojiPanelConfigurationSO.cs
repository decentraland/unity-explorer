using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

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
        public EmojiSectionName sectionName;
        public List<SerializableKeyValuePair<string, string>> ranges;
    }

    public enum EmojiSectionName
    {
        SMILEYS_AND_PEOPLE,
        ANIMALS_AND_NATURE,
        FOODS_AND_DRINKS,
        ACTIVITIES,
        TRAVEL_AND_PLACES,
        OBJECTS,
        SYMBOLS_AND_SIGNS,
        FLAGS
    }
}
