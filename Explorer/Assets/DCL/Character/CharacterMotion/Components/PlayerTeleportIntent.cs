using DCL.Ipfs;
using DCL.Utilities;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public struct PlayerTeleportIntent
    {
        public readonly struct JustTeleported
        {
            public readonly int ExpireFrame;
            public readonly Vector2Int Parcel;

            public JustTeleported(int expireFrame, Vector2Int parcel)
            {
                ExpireFrame = expireFrame;
                Parcel = parcel;
            }
        }

        private static readonly TimeSpan TIMEOUT = TimeSpan.FromMinutes(2);
        private readonly float creationTime;

        public readonly Vector2Int Parcel;
        public readonly CancellationToken CancellationToken;
        public readonly SceneEntityDefinition? SceneDef;
        public bool IsPositionSet;
        public Vector3 Position;

        /// <summary>
        ///     When true, land at <see cref="Parcel" /> itself instead of the scene's spawn point
        ///     (e.g. jumping into an event located at a specific parcel of a multi-parcel scene).
        /// </summary>
        public bool LandOnParcel;

        /// <summary>
        ///     Strictly it's the same report added to "SceneReadinessReportQueue" <br />
        ///     Teleport operation will wait for this report to be resolved before finishing the teleport operation <br />
        ///     Otherwise the teleport operation will be executed immediately
        /// </summary>
        public readonly AsyncLoadProcessReport? AssetsResolution;

        public bool TimedOut => UnityEngine.Time.realtimeSinceStartup - creationTime > TIMEOUT.TotalSeconds;

        public PlayerTeleportIntent(SceneEntityDefinition? sceneDef, Vector2Int parcel, Vector3 position, CancellationToken cancellationToken, AsyncLoadProcessReport? assetsResolution = null, bool isPositionSet = false, bool landOnParcel = false)
        {
            Parcel = parcel;
            CancellationToken = cancellationToken;
            AssetsResolution = assetsResolution;
            creationTime = UnityEngine.Time.realtimeSinceStartup;
            SceneDef = sceneDef;
            IsPositionSet = isPositionSet;
            Position = position;
            LandOnParcel = landOnParcel;
        }
    }
}
