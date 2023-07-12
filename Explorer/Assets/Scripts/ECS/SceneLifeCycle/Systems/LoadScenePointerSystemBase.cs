using Arch.Core;
using ECS.Abstract;
using ECS.SceneLifeCycle.SceneDefinition;
using Ipfs;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    public abstract class LoadScenePointerSystemBase : BaseUnityLoopSystem
    {
        protected LoadScenePointerSystemBase(World world) : base(world) { }

        protected Entity CreateSceneEntity(IpfsTypes.SceneEntityDefinition definition, IpfsTypes.IpfsPath ipfsPath, out Vector2Int[] parcels)
        {
            parcels = new Vector2Int[definition.metadata.scene.parcels.Count];

            for (var i = 0; i < definition.metadata.scene.parcels.Count; i++)
                parcels[i] = IpfsHelper.DecodePointer(definition.metadata.scene.parcels[i]);

            return World.Create(new SceneDefinitionComponent(definition, parcels, ipfsPath));
        }
    }
}
