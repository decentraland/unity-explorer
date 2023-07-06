using Arch.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ECS.Editor
{
    public class EditorSceneMonitor : IEditorSceneMonitor
    {
        public static IEditorSceneMonitor Instance { get; } = instance ??= new EditorSceneMonitor();
        private static IEditorSceneMonitor instance;

        private Dictionary<string, World> ScenesDictionary { get; } = new Dictionary<string, World>();

        public void Register(string originScene, World world)
        {
            if (!ScenesDictionary.TryAdd(originScene, world))
            {
                ScenesDictionary[originScene] = world;
            }
        }

        public void Unregister(string originScene)
        {
            ScenesDictionary.Remove(originScene);
        }

        public void Tick()
        {
            OnUpdate?.Invoke();
        }

        public Dictionary<string, World> GetScenes() => ScenesDictionary;

        public event OnUpdateEventHandler OnUpdate;
    }
}
