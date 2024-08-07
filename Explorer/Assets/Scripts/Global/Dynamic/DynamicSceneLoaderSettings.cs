using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Global.Dynamic
{
    [CreateAssetMenu(fileName = "DynamicSceneLoaderSettings", menuName = "SO/DynamicSceneLoaderSettings")]
    public class DynamicSceneLoaderSettings : ScriptableObject
    {
        private const string ENV_PARAM = "dclenv";

        [field: SerializeField] public DecentralandEnvironment DecentralandEnvironment { get; private set; }
        [field: SerializeField] public List<string> Realms { get; private set; }
        [field: SerializeField] public List<string> Web3WhitelistMethods { get; private set; }

        public void ApplyConfig(ApplicationParametersParser applicationParametersParser)
        {
            Dictionary<string,string> applicationParams = applicationParametersParser.Get();

            if (applicationParams.TryGetValue(ENV_PARAM, out string environment))
                ParseEnvironment(environment);
        }

        private void ParseEnvironment(string environment)
        {
            if (Enum.TryParse(environment, out DecentralandEnvironment env))
                DecentralandEnvironment = env;
        }
    }
}
