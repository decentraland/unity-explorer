using System.Collections.Generic;

namespace EntitiesDebugTool.Runtime.Snapshots
{
    public interface IWorldEntitiesSnapshot
    {
        void TakeSnapshot();

        IReadOnlyList<(int id, object[])>? Entities();

        class Fake : IWorldEntitiesSnapshot
        {
            private readonly IReadOnlyList<(int id, object[])> entities;
            private bool isSnapshotTaken;

            public Fake(IReadOnlyList<(int id, object[])> entities)
            {
                this.entities = entities;
            }

            public void TakeSnapshot()
            {
                isSnapshotTaken = true;
            }

            public IReadOnlyList<(int id, object[])>? Entities() =>
                isSnapshotTaken ? entities : null;
        }
    }
}
