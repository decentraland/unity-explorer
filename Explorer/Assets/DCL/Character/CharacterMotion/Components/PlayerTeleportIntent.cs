using DCL.Utilities;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public readonly struct PlayerTeleportIntent
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

        public static readonly TimeSpan TIMEOUT = TimeSpan.FromMinutes(2);

        public readonly Vector2Int Parcel;
        public readonly Vector3 Position;

        public readonly CancellationToken CancellationToken;

        /// <summary>
        ///     Strictly it's the same report added to "SceneReadinessReportQueue" <br />
        ///     Teleport operation will wait for this report to be resolved before finishing the teleport operation <br />
        ///     Otherwise the teleport operation will be executed immediately
        /// </summary>
        public readonly AsyncLoadProcessReport? AssetsResolution;

        public readonly float CreationTime;

        public PlayerTeleportIntent(Vector3 position, Vector2Int parcel, CancellationToken cancellationToken, AsyncLoadProcessReport? assetsResolution = null)
        {
            Position = position;
            Parcel = parcel;
            CancellationToken = cancellationToken;
            AssetsResolution = assetsResolution;
            CreationTime = Time.realtimeSinceStartup;
        }

        public bool TimedOut => Time.realtimeSinceStartup - CreationTime > TIMEOUT.TotalSeconds;
    }
}
