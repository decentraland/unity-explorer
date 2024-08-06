using DCL.Multiplayer.Connections.DecentralandUrls;
using System.Collections.Generic;
using UnityEngine;

namespace Global.Dynamic
{
    [CreateAssetMenu(fileName = "DynamicSceneLoaderSettings", menuName = "SO/DynamicSceneLoaderSettings")]
    public class DynamicSceneLoaderSettings : ScriptableObject
    {
        [field: SerializeField] public DecentralandEnvironment DecentralandEnvironment { get; private set; }
        [field: SerializeField] public List<string> Realms { get; private set; }
        [field: SerializeField] public List<string> Web3WhitelistMethods { get; private set; }
    }
}
