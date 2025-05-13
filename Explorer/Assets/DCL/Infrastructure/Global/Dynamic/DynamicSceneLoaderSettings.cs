using DCL.Multiplayer.Connections.DecentralandUrls;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Global.Dynamic
{
    [CreateAssetMenu(fileName = "DynamicSceneLoaderSettings", menuName = "DCL/Various/Dynamic Scene LoaderSettings")]
    public class DynamicSceneLoaderSettings : ScriptableObject
    {
        [field: SerializeField] public List<string> Realms { get; private set; }
        [field: SerializeField] public List<string> Web3WhitelistMethods { get; private set; }
        [field: SerializeField] public List<string> Web3ReadOnlyMethods { get; private set; }
    }
}
