using JetBrains.Annotations;

namespace Decentraland.Common
{
    public partial class Vector3
    {
        public static implicit operator UnityEngine.Vector3([CanBeNull] Vector3 v) =>
            v == null ? UnityEngine.Vector3.zero : new UnityEngine.Vector3(v.X, v.Y, v.Z);
    }

    public partial class Vector2
    {
        public static implicit operator UnityEngine.Vector2([CanBeNull] Vector2 v) =>
            v == null ? UnityEngine.Vector3.zero : new UnityEngine.Vector2(v.X, v.Y);
    }
}
