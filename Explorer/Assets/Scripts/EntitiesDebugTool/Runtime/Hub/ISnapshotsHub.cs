using EntitiesDebugTool.Runtime.Snapshots;
using System;
using System.Collections.Generic;

namespace EntitiesDebugTool.Runtime.Hub
{
    public interface ISnapshotsHub
    {
        IEnumerable<string> AvailableWorlds();

        IWorldEntitiesSnapshot Snapshot(string worldName);

        class Fake : ISnapshotsHub
        {
            private readonly IReadOnlyDictionary<string, IWorldEntitiesSnapshot> map;

            public Fake() : this(
                new Dictionary<string, IWorldEntitiesSnapshot>
                {
                    ["main world"] = new IWorldEntitiesSnapshot.Fake(new List<(int id, object[])>
                        {
                            (100, new object[] { "hello there", 100, "Just a compo" }),
                            (551, new object[] { "hello again" }),
                            (5151, new object[] { "hello why" }),
                            (4214, new object[] { "tell it" }),
                            (612, new object[] { "hello there" }),
                        }
                    ),
                    ["another world"] = new IWorldEntitiesSnapshot.Fake(new List<(int id, object[])>
                        {
                            (100, new object[] { "hello there", 100, "Just a compo" }),
                            (551, new object[] { "hello again" }),
                            (5151, new object[] { "hello why" }),
                            (4214, new object[] { "tell it" }),
                            (612, new object[] { "hello there" }),
                        }
                    ),
                }
            ) { }

            public Fake(IReadOnlyDictionary<string, IWorldEntitiesSnapshot> map)
            {
                this.map = map;
            }

            public IEnumerable<string> AvailableWorlds() =>
                map.Keys;

            public IWorldEntitiesSnapshot Snapshot(string worldName)
            {
                if (map.TryGetValue(worldName, out var result))
                    return result!;

                throw new Exception("World name doesn't exists");
            }
        }
    }
}
