using System.Collections.Generic;

namespace ECS.Abstract
{
    /// <summary>
    ///     Simple group without arguments and with a Single Update
    /// </summary>
    public class BaseSystemsGroup : ISystem
    {
        private readonly List<ISystem> systems;

        public BaseSystemsGroup(IEnumerable<ISystem> systems)
        {
            this.systems = new List<ISystem>(systems);
        }

        public BaseSystemsGroup(params ISystem[] systems)
        {
            this.systems = new List<ISystem>(systems);
        }

        public void Initialize()
        {
            for (var index = 0; index < systems.Count; ++index)
                systems[index].Initialize();
        }

        public void Dispose()
        {
            for (var index = 0; index < systems.Count; ++index)
                systems[index].Dispose();
        }

        public void Update()
        {
            for (var index = 0; index < systems.Count; ++index)
                systems[index].Update();
        }
    }
}
