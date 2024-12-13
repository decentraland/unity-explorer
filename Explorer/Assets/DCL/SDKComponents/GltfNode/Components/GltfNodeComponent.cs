using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.GltfNode.Components
{
    public struct GltfNodeComponent
    {
        public GameObject originalNodeGameObject;
        public Transform clonedNodeTransform;
        public readonly HashSet<string> originalNodeChildrenNames;

        public GltfNodeComponent(GameObject originalNodeGO, Transform clonedNode)
        {
            originalNodeGameObject = originalNodeGO;
            originalNodeChildrenNames = new HashSet<string>();
            foreach (Transform child in originalNodeGameObject.transform)
            {
                originalNodeChildrenNames.Add(child.name);
            }

            clonedNodeTransform = clonedNode;
        }
    }
}
