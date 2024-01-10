using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class BackpackSortDropdownView : MonoBehaviour
    {
        [field: SerializeField]
        internal Toggle sortNewest { get; private set; }

        [field: SerializeField]
        internal Toggle sortOldest { get; private set; }

        [field: SerializeField]
        internal Toggle sortRarest { get; private set; }

        [field: SerializeField]
        internal Toggle sortLessRares { get; private set; }

        [field: SerializeField]
        internal Toggle sortNameAz { get; private set; }

        [field: SerializeField]
        internal Toggle sortNameZa { get; private set; }

        [field: SerializeField]
        internal Button sortDropdownButton { get; private set; }

        [field: SerializeField]
        internal RectTransform sortContent { get; private set; }

        private void Start()
        {
            sortDropdownButton.onClick.AddListener(OnSortDropdownClick);
            sortContent.gameObject.SetActive(false);
        }

        private void OnSortDropdownClick()
        {
            if (sortContent.gameObject.activeInHierarchy)
            {
                sortContent.gameObject.SetActive(false);
            }
            else
            {
                sortContent.gameObject.SetActive(true);
            }
        }
    }
}
