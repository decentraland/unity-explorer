using DCL.AsyncLoadReporting;
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

            public JustTeleported(int expireFrame)
            {
                ExpireFrame = expireFrame;
            }
        }

        public static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(30);

        public readonly Vector2Int Parcel;
        public readonly Vector3 Position;

        public readonly CancellationToken CancellationToken;

        /// <summary>
        ///     Strictly it's the same report added to "SceneReadinessReportQueue"
        /// </summary>
        public readonly AsyncLoadProcessReport? LoadReport;
        public readonly float CreationTime;

        public PlayerTeleportIntent(Vector3 position, Vector2Int parcel, CancellationToken cancellationToken, AsyncLoadProcessReport? loadReport = null)
        {
            Position = position;
            Parcel = parcel;
            CancellationToken = cancellationToken;
            LoadReport = loadReport;
            CreationTime = Time.realtimeSinceStartup;
        }

        public bool TimedOut => Time.realtimeSinceStartup - CreationTime > TIMEOUT.TotalSeconds;
    }
}
