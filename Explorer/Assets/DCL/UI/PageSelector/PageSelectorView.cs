using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class PageSelectorView : MonoBehaviour
    {
        [field: SerializeField]
        public Button NextPage { get; private set; }

        [field: SerializeField]
        public Button PreviousPage { get; private set; }

        [field: SerializeField]
        public Transform PagesContainer { get; private set; }

        [field: SerializeField]
        public GameObject PageReference { get; private set; }
    }
}
