using Decentraland.Common;
using System.Diagnostics.CodeAnalysis;

namespace CrdtEcsBridge.Components.ResetExtensions
{
    /// <summary>
    ///     Custom logic to initialize components to default values without fields reallocation
    /// </summary>
    public static class PrimitivesInitializationExtensions
    {
        public static void Reset(this Vector3 protoVector) =>
            protoVector.Set(UnityEngine.Vector3.zero);

        public static void Set([NotNull] this Vector3 protoVector, UnityEngine.Vector3 unityVector)
        {
            protoVector.X = unityVector.x;
            protoVector.Y = unityVector.y;
            protoVector.Z = unityVector.z;
        }
    }
}
