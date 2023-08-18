using Arch.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ECS.Editor
{
    public class EcsMonitor : IEcsMonitor
    {
        public static IEcsMonitor Instance { get; } = instance ??= new EcsMonitor();
        private static IEcsMonitor instance;

        private Dictionary<string, World> Scenes { get; } = new ();

        public void Register(string originScene, World world)
        {
            if (!Scenes.TryAdd(originScene, world))
            {
                Scenes[originScene] = world;
            }
        }

        public void Unregister(string originScene)
        {
            Scenes.Remove(originScene);
        }

        public void Tick()
        {
            // Update Any Registered Event Listeners
            OnUpdate?.Invoke();
        }

        public Dictionary<string, World> GetScenes() => Scenes;

        public event OnUpdateEventHandler OnUpdate;
    }
}
