using System.Collections.Generic;
using UnityEngine;

namespace Global.Dynamic
{
    [CreateAssetMenu(fileName = "DynamicSceneLoaderSettings", menuName = "SO/DynamicSceneLoaderSettings")]
    public class DynamicSceneLoaderSettings : ScriptableObject
    {
        [field: SerializeField] public List<string> Realms { get; private set; }
        [field: SerializeField] public string AuthWebSocketUrl { get; private set; }
        [field: SerializeField] public string AuthWebSocketUrlDev { get; private set; }
        [field: SerializeField] public string AuthSignatureUrl { get; private set; }
        [field: SerializeField] public string AuthSignatureUrlDev { get; private set; }
        [field: SerializeField] public List<string> Web3WhitelistMethods { get; private set; }
    }
}
