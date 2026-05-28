using DCL.SceneRunner.Scene;
using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     Immutable result of resolving a scene's Initial Scene State: the final state
    ///     (Bundle / Descriptor / None) and the descriptor asset list. This is the <c>TAsset</c>
    ///     produced by <see cref="LoadISSDescriptorSystem"/> and stored in the disk cache —
    ///     pure data, shareable across consumers, no per-scene runtime state.
    ///     <para>
    ///     <see cref="ResolveISSDescriptorSystem"/> hands it off to the entity's
    ///     <see cref="ISSDescriptor"/> component via <see cref="ISSDescriptor.MarkResolved"/>;
    ///     bridge-slot bookkeeping and lifecycle live on the component, not here.
    ///     </para>
    /// </summary>
    public readonly struct ISSDescriptorResolution
    {
        public static readonly ISSDescriptorResolution NONE = new (IISSDescriptor.State.None, null);

        public readonly IISSDescriptor.State State;
        public readonly IReadOnlyList<ISSDescriptorAsset>? Assets;

        public ISSDescriptorResolution(IISSDescriptor.State state, IReadOnlyList<ISSDescriptorAsset>? assets)
        {
            State = state;
            Assets = assets;
        }
    }
}
