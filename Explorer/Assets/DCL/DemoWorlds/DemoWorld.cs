using Arch.System;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Time = UnityEngine.Time;

namespace DCL.DemoWorlds
{
    public class DemoWorld : IDemoWorld
    {
        private readonly Func<Arch.Core.World> worldConstructor;
        private readonly IReadOnlyList<Func<Arch.Core.World, BaseSystem<Arch.Core.World, float>>> systemConstructors;
        private readonly Action<Arch.Core.World> setUp;

        private IReadOnlyList<BaseSystem<Arch.Core.World, float>> systems = new[] { new FakeSystem() };

        public DemoWorld(Action<Arch.Core.World> setUp, params Func<Arch.Core.World, BaseSystem<Arch.Core.World, float>>[] systemConstructors) : this(systemConstructors.AsReadOnly(), setUp) { }

        public DemoWorld(IReadOnlyList<Func<Arch.Core.World, BaseSystem<Arch.Core.World, float>>> systemConstructors, Action<Arch.Core.World> setUp) : this(Arch.Core.World.Create, systemConstructors, setUp) { }

        public DemoWorld(Arch.Core.World world, Action<Arch.Core.World> setUp, params Func<Arch.Core.World, BaseSystem<Arch.Core.World, float>>[] systemConstructors) : this(() => world, systemConstructors.AsReadOnly(), setUp) { }

        public DemoWorld(Arch.Core.World world, IReadOnlyList<Func<Arch.Core.World, BaseSystem<Arch.Core.World, float>>> systemConstructors, Action<Arch.Core.World> setUp) : this(() => world, systemConstructors, setUp) { }

        public DemoWorld(Func<Arch.Core.World> worldConstructor, IReadOnlyList<Func<Arch.Core.World, BaseSystem<Arch.Core.World, float>>> systemConstructors, Action<Arch.Core.World> setUp)
        {
            this.worldConstructor = worldConstructor;
            this.systemConstructors = systemConstructors;
            this.setUp = setUp;
        }

        public void SetUp()
        {
            var world = worldConstructor();
            systems = systemConstructors.Select(e => e(world)).ToList();
            setUp(world);
        }

        public void Update()
        {
            foreach (var system in systems)
                system.Update(Time.deltaTime);
        }

        private class FakeSystem : BaseSystem<Arch.Core.World, float>
        {
            public FakeSystem() : this(Arch.Core.World.Create())
            {
            }

            public FakeSystem(Arch.Core.World world) : base(world) { }

            public override void Update(in float t)
            {
                throw new Exception("I am fake!, set up world first");
            }
        }
    }
}
