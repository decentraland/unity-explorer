using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PluginSystem.Validatables;
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

        public async UniTask EnsureValidAsync()
        {
            var list = new List<Exception>();
            var checkedCount = 0;

            async UniTask CheckAsync(IValidatableAsset dclPluginSettings)
            {
                ReportHub.Log(ReportData.UNSPECIFIED, $"Start check for {dclPluginSettings.GetType().FullName}");
                var exception = await dclPluginSettings.InvalidValuesAsync();
                checkedCount++;
                ReportHub.Log(ReportData.UNSPECIFIED, $"Finish check {checkedCount}/{settings.Count} {dclPluginSettings.GetType().FullName}");

                if (exception != null)
                    list.Add(exception);
            }

            await UniTask.WhenAll(
                Enumerable.Select(settings, CheckAsync)
            );

            if (list.Any())
                throw new AggregateException("Some settings are not valid", list);
        }
    }
}
