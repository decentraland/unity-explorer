using Arch.Core;
using EntitiesDebugTool.Runtime.Snapshots;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EntitiesDebugTool.Runtime.Hub
{
    public class SnapshotsHub : ISnapshotsHub
    {
        public IEnumerable<string> AvailableWorlds() =>
            Enumerable.Range(1, World.WorldSize).Select(e => e.ToString());


        public IWorldEntitiesSnapshot Snapshot(string worldName)
        {
            throw new NotImplementedException();
        }

    }
}
