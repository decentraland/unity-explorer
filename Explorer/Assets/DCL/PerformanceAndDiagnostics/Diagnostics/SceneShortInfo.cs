using UnityEngine;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Brief information about a scene that can be used to display it as a reference
    /// </summary>
    public readonly struct SceneShortInfo
    {
        public readonly Vector2Int BaseParcel;
        public readonly string Name;

        public SceneShortInfo(Vector2Int baseParcel, string name)
        {
            BaseParcel = baseParcel;
            Name = name;
        }

        public override string ToString() =>
            $"({BaseParcel.x},{BaseParcel.y}) - {Name}";
    }
}
