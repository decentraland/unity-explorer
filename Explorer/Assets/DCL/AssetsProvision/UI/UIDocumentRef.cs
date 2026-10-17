using System;
using UnityEngine.UIElements;

namespace DCL.AssetsProvision
{
    [Serializable]
    public class UIDocumentRef : ComponentReference<UIDocument>
    {
        public UIDocumentRef(string guid) : base(guid) { }
    }
}
