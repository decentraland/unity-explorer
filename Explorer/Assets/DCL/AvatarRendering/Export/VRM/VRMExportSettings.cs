using UnityEngine;
using VRM;

namespace DCL.AvatarRendering.Export
{
    public class VRMExportSettings
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Reference { get; set; }
        public GameObject toExport;
        public Transform bonesRoot;
        public Transform meshesContainer;
        public Material vrmToonMaterial;
        public Material vrmUnlitMaterial;
        public VRMMetaObject metaObject;
    }
}
