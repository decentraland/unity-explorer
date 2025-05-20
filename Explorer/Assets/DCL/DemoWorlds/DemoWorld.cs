using Arch.Core;
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
        private readonly Func<World> worldConstructor;
        private readonly IReadOnlyList<Func<World, BaseSystem<World, float>>> systemConstructors;
        private readonly Action<World> setUp;

        private IReadOnlyList<BaseSystem<World, float>> systems = new[] { new FakeSystem() };

        public DemoWorld(Action<World> setUp, params Func<World, BaseSystem<World, float>>[] systemConstructors) : this(systemConstructors.AsReadOnly(), setUp) { }

        public DemoWorld(IReadOnlyList<Func<World, BaseSystem<World, float>>> systemConstructors, Action<World> setUp) : this(() => World.Create(), systemConstructors, setUp) { }

        public DemoWorld(World world, Action<World> setUp, params Func<World, BaseSystem<World, float>>[] systemConstructors) : this(() => world, systemConstructors.AsReadOnly(), setUp) { }

        public DemoWorld(World world, IReadOnlyList<Func<World, BaseSystem<World, float>>> systemConstructors, Action<World> setUp) : this(() => world, systemConstructors, setUp) { }

        public DemoWorld(Func<World> worldConstructor, IReadOnlyList<Func<World, BaseSystem<World, float>>> systemConstructors, Action<World> setUp)
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

        private class FakeSystem : BaseSystem<World, float>
        {
            public FakeSystem() : this(World.Create())
            {
            }

            public FakeSystem(World world) : base(world) { }

            public override void Update(in float t)
            {
                throw new Exception("I am fake!, set up world first");
            }
        }
    }
}
