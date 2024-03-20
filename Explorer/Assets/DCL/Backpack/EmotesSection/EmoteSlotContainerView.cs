using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class EmoteSlotContainerView : MonoBehaviour
    {
        [field: SerializeField]
        internal Image FocusedImage { get; private set; }

        [field: SerializeField]
        public Image BackgroundRarity { get; private set; }

        [field: SerializeField]
        public TMP_Text EmoteName { get; private set; }
    }
}
