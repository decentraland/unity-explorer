using Arch.Core;
using System;
using System.Collections.Generic;

namespace EntitiesDebugTool.Runtime.Snapshots
{
    public class WorldEntitiesSnapshot : IWorldEntitiesSnapshot
    {
        private int worldId;

        public void TakeSnapshot()
        {
            var world = World.Worlds[worldId];
            //world.GetEntities();
            //TODO
            throw new NotImplementedException();
        }

        public IReadOnlyList<(int id, object[])>? Entities() =>
            throw new NotImplementedException();
    }
}
