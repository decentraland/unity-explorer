using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Settings
{
    [CreateAssetMenu(menuName = "Settings Menu/Create Settings Modules Mapping", fileName = "Settings Modules Mapping", order = 0)]
    public class SettingsModulesMapping : ScriptableObject
    {
        [field: SerializeField] public List<SettingsModuleMap> SettingsModules { get; set; }

        public SettingsModuleView GetModuleView(SettingsModuleType moduleType)
        {
            foreach (var settingsModule in SettingsModules)
            {
                if (settingsModule.moduleType == moduleType)
                    return settingsModule.moduleView;
            }

            return null;
        }
    }

    [Serializable]
    public class SettingsModuleMap
    {
        public SettingsModuleType moduleType;
        public SettingsModuleView moduleView;
    }
}
