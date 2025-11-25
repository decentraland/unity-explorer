using UnityEngine;
using VRM;

namespace DCL.AvatarRendering.Export
{
    public class VRMExporterReferences
    {
        public GameObject toExport;
        public Transform bonesRoot;
        public Transform meshesContainer;
        public Material vrmToonMaterial;
        public Material vrmUnlitMaterial;
        public VRMMetaObject metaObject;
    }
}
