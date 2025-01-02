using DCL.Multiplayer.Connections.DecentralandUrls;
using Global.AppArgs;
using System;
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

        public void ApplyConfig(IAppArgs applicationParametersParser)
        {
            if (applicationParametersParser.TryGetValue(AppArgsFlags.ENVIRONMENT, out string? environment))
                ParseEnvironment(environment!);
        }

        private void ParseEnvironment(string environment)
        {
            if (Enum.TryParse(environment, true, out DecentralandEnvironment env))
                DecentralandEnvironment = env;
        }
    }
}
