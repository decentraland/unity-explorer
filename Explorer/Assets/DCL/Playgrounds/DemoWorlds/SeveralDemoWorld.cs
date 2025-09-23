using DCL.Utilities.Extensions;
using System.Collections.Generic;

namespace DCL.DemoWorlds
{
    public class SeveralDemoWorld : IDemoWorld
    {
        private readonly IReadOnlyList<IDemoWorld> worlds;

        public SeveralDemoWorld(params IDemoWorld[] worlds) : this(worlds.AsReadOnly()) { }

        public SeveralDemoWorld(IReadOnlyList<IDemoWorld> worlds)
        {
            this.worlds = worlds;
        }

        public void SetUp()
        {
            foreach (var demoWorld in worlds)
                demoWorld.SetUp();
        }

        public void Update()
        {
            foreach (var demoWorld in worlds)
                demoWorld.Update();
        }
    }
}
