using UnityEngine;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Parsed target of the /goto command, produced by <see cref="ChatParamUtils.ParseGotoTarget" />.
    /// Exactly one shape is set: a Genesis parcel (<see cref="Parcel" />, optionally with
    /// <see cref="SpawnPoint" />), a world (<see cref="World" />, optionally with <see cref="Parcel" />),
    /// or one of the special flags <see cref="IsRandom" /> / <see cref="IsCrowd" />.
    /// </summary>
    public readonly struct GotoTarget
    {
        public readonly string? World;
        public readonly Vector2Int? Parcel;
        public readonly string? SpawnPoint;
        public readonly bool IsRandom;
        public readonly bool IsCrowd;

        public GotoTarget(string? world, Vector2Int? parcel, string? spawnPoint, bool isRandom = false, bool isCrowd = false)
        {
            World = world;
            Parcel = parcel;
            SpawnPoint = spawnPoint;
            IsRandom = isRandom;
            IsCrowd = isCrowd;
        }
    }
}
