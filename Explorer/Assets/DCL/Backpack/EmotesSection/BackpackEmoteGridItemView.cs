using TMPro;
using UnityEngine;

namespace DCL.Backpack.EmotesSection
{
    public class BackpackEmoteGridItemView : BackpackItemView
    {
        [field: SerializeField]
        public TMP_Text EquippedSlotLabel { get; private set; }
    }
}
