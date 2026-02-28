#if UNITY_WEBGL //&& !UNITY_EDITOR

using Arch.Core;
using Arch.System;
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
        private readonly IWebGLSceneUpdateQueue webglSceneUpdateQueue;

        internal ProcessWebGLSceneUpdatesSystem(World world, IWebGLSceneUpdateQueue webglSceneUpdateQueue) : base(world)
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
