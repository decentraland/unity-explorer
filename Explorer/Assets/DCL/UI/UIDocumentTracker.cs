using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class UIDocumentTracker: MonoBehaviour
    {
        public static IReadOnlyList<UIDocument> ActiveDocuments => trackedDocuments;

        private static readonly List<UIDocument> trackedDocuments = new ();

        private void OnEnable()
        {
            trackedDocuments.Add(GetComponent<UIDocument>());
        }

        private void OnDisable()
        {
            trackedDocuments.Remove(GetComponent<UIDocument>());
        }
    }
}
