using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SceneRunner.Scene
{
    /// <summary>
    ///     The Initial Scene State for a scene. Defined here (in <c>SceneRunner.Scene</c>) so
    ///     <see cref="ISceneData"/> can hold it without dragging the ECS asmdef into the layer
    ///     graph. The concrete implementation (with the typed asset-bundle handle and the
    ///     <c>AttachAssetBundle</c> hook used at scene load) lives next to the AB loader in
    ///     <c>ECS.StreamableLoading.AssetBundles.InitialSceneState</c>.
    /// </summary>
    public interface IISSDescriptor
    {
        State CurrentState { get; }
        IReadOnlyList<ISSDescriptorAsset> Assets { get; }

        /// <summary>
        ///     Reserves a bridge slot for <paramref name="hash"/>; caps at the descriptor's per-hash count
        ///     so the bridge never holds more copies of an asset than the scene needs.
        /// </summary>
        bool TryReserveBridgeSlot(string hash);

        void ReleaseBridgeSlot(string hash);

        enum State
        {
            /// <summary>
            ///     The descriptor has not yet been resolved. Radius-gate spawns the resolver promise when it sees this state.
            /// </summary>
            Uninitialized,
            None,
            Bundle,
            Descriptor,
        }
    }

    [Serializable]
    public struct ISSDescriptorAsset
    {
        public string hash;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }
}
