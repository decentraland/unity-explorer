using System.Collections.Generic;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
public class StaticSceneDescriptor
{
    public List<string> assetHash = new();
    public List<Vector3> positions = new();
    public List<Quaternion> rotations = new();
    public List<Vector3> scales = new();
}
}
