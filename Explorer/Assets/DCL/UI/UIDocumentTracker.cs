using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class UIDocumentTracker: MonoBehaviour
    {
        public static IReadOnlyList<UIDocumentTracker> ActiveDocuments => trackedDocuments;
        private static readonly List<UIDocumentTracker> trackedDocuments = new ();

        [field: SerializeField]
        public bool CanBeHidden { get; private set; } = true;

        public UIDocument Document { get; private set; }

        private void Awake()
        {
            Document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            trackedDocuments.Add(this);
        }

        private void OnDisable()
        {
            trackedDocuments.Remove(this);
        }
    }
}
