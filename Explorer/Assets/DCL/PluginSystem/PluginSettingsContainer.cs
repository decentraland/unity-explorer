﻿using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PluginSystem.Validatables;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.PluginSystem
{
    /// <summary>
    ///     Stores settings for plugins in a scriptable object
    /// </summary>
    [CreateAssetMenu(fileName = "Plugins Container", menuName = "DCL/Plugins/Plugins Container")]
    public class PluginSettingsContainer : ScriptableObject, IPluginSettingsContainer
    {
        // We should initialize this by a custom inspector
        // ReSharper disable once CollectionNeverUpdated.Global
        [SerializeReference] [PluginSettingsTitle] internal List<IDCLPluginSettings> settings = new ();

        public object GetSettings(Type type)
        {
            if (type == typeof(NoExposedPluginSettings))
                return NoExposedPluginSettings.Instance;

            try { return settings.Find(x => x.GetType() == type).EnsureNotNull(); }
            catch (Exception e) { throw new NullReferenceException($"Settings not found for type {type.Name} at {GetType().FullName}", e); }
        }

        public async UniTask EnsureValidAsync()
        {
            var list = new List<Exception>();
            var checkedCount = 0;

            async UniTask CheckAsync(IValidatableAsset dclPluginSettings)
            {
                ReportHub.Log(ReportData.UNSPECIFIED, $"Start check for {dclPluginSettings.GetType().FullName}");
                AggregateException? exception = await dclPluginSettings.InvalidValuesAsync();
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
