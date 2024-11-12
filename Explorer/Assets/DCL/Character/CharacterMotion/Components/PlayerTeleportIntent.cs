using DCL.AsyncLoadReporting;
using System;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public readonly struct PlayerTeleportIntent
    {
        public static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(30);

        public readonly Vector2Int Parcel;
        public readonly Vector3 Position;
        public readonly AsyncLoadProcessReport? LoadReport;
        public readonly float CreationTime;

        public PlayerTeleportIntent(Vector3 position, Vector2Int parcel, AsyncLoadProcessReport? loadReport = null)
        {
            Position = position;
            Parcel = parcel;
            LoadReport = loadReport;
            CreationTime = Time.realtimeSinceStartup;
        }

        public bool IsExpired => Time.realtimeSinceStartup - CreationTime > TIMEOUT.TotalSeconds;
    }
}
