using UnityEngine;

namespace DCL.BevyMigration.Bridge
{
    /// <summary>
    ///     Marks a renderer as drawn through Bevy while the bridge is active: registered on enable, unregistered on
    ///     disable. Step 2 attaches it by hand (the test cube); step 3 registers scene content from its systems.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class BevyRenderedObject : MonoBehaviour
    {
        private uint handle;
        private bool registered;

        public uint Handle => handle;

        private void OnEnable()
        {
            RendererRegistry? registry = BevyBridgeRuntime.Registry;
            if (registry == null) return;

            handle = registry.Register(GetComponent<Renderer>());
            registered = true;
        }

        private void OnDisable()
        {
            if (!registered) return;

            BevyBridgeRuntime.Registry?.Unregister(handle);
            registered = false;
        }
    }

    /// <summary>
    ///     The registry of the running bridge, set by the plugin for the lifetime of the global world so that
    ///     scene objects can register without a reference to the plugin.
    /// </summary>
    public static class BevyBridgeRuntime
    {
        public static RendererRegistry? Registry { get; set; }
    }
}
