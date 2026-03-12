#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)

using Arch.Core;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.SceneLifeCycle.WebGL;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Drains the WebGL scene update queue before scene worlds run, ensuring CRDT is applied before UpdateTransformSystem.
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(ControlSceneUpdateLoopSystem))]
    public partial class ProcessWebGLSceneUpdatesSystem : BaseUnityLoopSystem
    {
        private readonly WebGLSceneUpdateQueue webglSceneUpdateQueue;

        internal ProcessWebGLSceneUpdatesSystem(World world, WebGLSceneUpdateQueue webglSceneUpdateQueue) : base(world)
        {
            this.webglSceneUpdateQueue = webglSceneUpdateQueue;
        }

        protected override void Update(float t)
        {
            webglSceneUpdateQueue.ProcessPendingUpdates();
        }
    }
}

#endif
