using Arch.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ECS.Editor
{
    public delegate void OnUpdateEventHandler();

    public interface IEcsMonitor
    {
        /// <summary>
        /// Registers a scene to be monitored by the editor
        /// </summary>
        void Register(string originScene, World world);

        /// <summary>
        /// Unregisters a scene to be monitored by the editor
        /// </summary>
        void Unregister(string originScene);

        /// <summary>
        /// Represents a tick of the editor, when the editor should be refreshed
        /// </summary>
        void Tick();

        /// <summary>
        /// Gets the scenes currently running
        /// </summary>
        Dictionary<string, World> GetScenes();

        /// <summary>
        /// Raised when the editor should be refreshed
        /// </summary>
        event OnUpdateEventHandler OnUpdate;
    }
}
