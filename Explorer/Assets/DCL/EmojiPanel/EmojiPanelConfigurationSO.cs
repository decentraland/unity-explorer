using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.Emoji
{
    [CreateAssetMenu(fileName = "EmojiPanelConfig", menuName = "DCL/Chat/Emoji Panel Config")]
    public class EmojiPanelConfigurationSO : ScriptableObject
    {
        [SerializeField] public List<EmojiSection> EmojiSections;
    }

    [Serializable]
    public class EmojiSection
    {
        public string title;
        public EmojiSectionName sectionName;
        public List<SerializableKeyValuePair<string, int>> emojis = new ();
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
        FLAGS,
    }
}
