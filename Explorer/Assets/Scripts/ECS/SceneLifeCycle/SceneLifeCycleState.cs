using Arch.Core;
using Ipfs;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle
{
    public class SceneLifeCycleState
    {
        public readonly Dictionary<string, Entity> LiveScenes = new ();

        /// <summary>
        ///     Scene pointers are cached and never removed
        /// </summary>
        public readonly Dictionary<Vector2Int, IpfsTypes.SceneEntityDefinition> ScenePointers = new ();

        public readonly Dictionary<string, Entity> LiveScenes = new ();

        public readonly HashSet<IpfsTypes.SceneEntityDefinition> FixedScenes = new ();

        public readonly List<IpfsTypes.IpfsPath> ScenesMetadataToLoad = new ();

        public IIpfsRealm IpfsRealm;

        public bool NewRealm = false;

        public Entity PlayerEntity;

        public int SceneLoadRadius;
    }
}
