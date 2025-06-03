using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Places
{
    public class AddPlaceItemView : MonoBehaviour
    {
        [field: SerializeField] private Button mainButton { get; set; }

        public event Action? MainButtonClicked;

        private void Awake()
        {
            mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke());
        }
    }
}
