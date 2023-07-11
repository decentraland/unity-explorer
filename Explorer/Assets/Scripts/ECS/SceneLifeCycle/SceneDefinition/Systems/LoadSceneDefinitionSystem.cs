using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Ipfs;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Loads a single scene definition from URN
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoadSceneDefinitionSystem : LoadSystemBase<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>
    {
        internal LoadSceneDefinitionSystem(World world, IStreamableCache<IpfsTypes.SceneEntityDefinition, GetSceneDefinition> cache, MutexSync mutexSync)
            : base(world, cache, mutexSync) { }

        protected override async UniTask<StreamableLoadingResult<IpfsTypes.SceneEntityDefinition>> FlowInternal(GetSceneDefinition intention, CancellationToken ct)
        {
            var wr = UnityWebRequest.Get(intention.CommonArguments.URL);
            await wr.SendWebRequest().WithCancellation(ct);

            // Get text on the main thread
            string text = wr.downloadHandler.text;
            await UniTask.SwitchToThreadPool();

            IpfsTypes.SceneEntityDefinition sceneEntityDefinition = JsonUtility.FromJson<IpfsTypes.SceneEntityDefinition>(text);
            sceneEntityDefinition.id ??= intention.IpfsPath.EntityId;

            // switching back is handled by the base class
            return new StreamableLoadingResult<IpfsTypes.SceneEntityDefinition>(sceneEntityDefinition);
        }
    }
}
