using DCL.PluginSystem.Validatables;
using DCL.Utilities.Addressables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.PluginSystem
{
    /// <summary>
    ///     Stores settings for plugins in a scriptable object
    /// </summary>
    [CreateAssetMenu(menuName = "Create Plugins Container", fileName = "Plugins Container", order = 0)]
    public class PluginSettingsContainer : ScriptableObject, IPluginSettingsContainer
    {
        // We should initialize this by a custom inspector
        // ReSharper disable once CollectionNeverUpdated.Global
        [SerializeReference] [PluginSettingsTitle] internal List<IDCLPluginSettings> settings;

        public T GetSettings<T>() where T: IDCLPluginSettings
        {
            var typeSettings = (T)settings.Find(x => x.GetType() == typeof(T));

            if (typeSettings == null)
                throw new NullReferenceException("Settings not found for type " + typeof(T).Name);

            return typeSettings;
        }

        public void EnsureValid()
        {
            var list = settings.Select(e => e.InvalidValues()).OfType<Exception>().ToList();

            if (list.Any())
                throw new AggregateException("Some settings are not valid", list);
        }
    }
}
