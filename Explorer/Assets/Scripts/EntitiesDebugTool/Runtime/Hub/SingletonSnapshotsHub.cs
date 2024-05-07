using EntitiesDebugTool.Runtime.Snapshots;
using System;
using System.Collections.Generic;

namespace EntitiesDebugTool.Runtime.Hub
{
    public class SingletonSnapshotsHub : ISnapshotsHub
    {
        private static ISnapshotsHub? instance;

        private static ISnapshotsHub Instanse()
        {
            if (instance == null)
                throw new Exception("SnapshotsHub is not injected. Please inject it using SnapshotsHub.Inject() method.");

            return instance;
        }

        public IEnumerable<string> AvailableWorlds() =>
            Instanse().AvailableWorlds();

        public IWorldEntitiesSnapshot Snapshot(string worldName) =>
            Instanse().Snapshot(worldName);

        public static void Inject(ISnapshotsHub snapshotsHub)
        {
            instance = snapshotsHub;
        }
    }
}
