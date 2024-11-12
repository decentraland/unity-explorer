using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.Diagnostics;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace ECS.Abstract
{
    /// <summary>
    ///     Provides additional functionality to `BaseSystem`
    /// </summary>
    public abstract class BaseUnityLoopSystem : PlayerLoopSystem<World>
    {
        private static readonly QueryDescription SCENE_INFO_QUERY = new QueryDescription().WithAll<SceneShortInfo>();

        private readonly CustomSampler updateSampler;

        /// <summary>
        ///     Individual profiler marker for each combination of generic arguments
        /// </summary>
        private readonly CustomSampler? genericUpdateSampler;

        private string? cachedCategory;

        protected readonly SceneShortInfo sceneInfo;

        protected BaseUnityLoopSystem(World world) : base(world)
        {
            updateSampler = CustomSampler.Create($"{GetType().Name}.Update");
            genericUpdateSampler = CreateGenericSamplerIfRequired();

            var entity = new SingleInstanceEntity(SCENE_INFO_QUERY, world, false);

            if (entity != Entity.Null)
                sceneInfo = world.Get<SceneShortInfo>(entity);
        }

        private CustomSampler? CreateGenericSamplerIfRequired()
        {
            Type type = GetType();
            return type.IsGenericType ? CustomSampler.Create($"{type.Name.Remove(type.Name.Length - 2)}<{string.Join(", ", type.GenericTypeArguments.Select(x => x.Name))}>.Update") : null;
        }

        public sealed override void Update(in float t)
        {
            try
            {
                updateSampler.Begin();

                genericUpdateSampler?.Begin();

                Update(t);

                genericUpdateSampler?.End();

                updateSampler.End();

            }
            catch (Exception e)
            {
                // enrich and propagate exception to the system group handler
                throw CreateException(e, ReportHint.None, true);
            }
        }

        protected abstract void Update(float t);

        protected internal ReportData GetReportData() =>
            new (GetReportCategory(), sceneShortInfo: sceneInfo);

        // Look for category starting from the class itself and then groups recursively
        // if not found fall back to "ECS"
        protected internal string GetReportCategory()
        {
            if (cachedCategory != null) return cachedCategory;

            AttributesInfoBase metadata = GetMetadataInternal();
            LogCategoryAttribute? logCategory = null;

            while (metadata != null && (logCategory = metadata.GetAttribute<LogCategoryAttribute>()) == null)
                metadata = metadata.GroupMetadata;

            cachedCategory = logCategory?.Category ?? ReportCategory.ECS;

            return cachedCategory;
        }

        /// <summary>
        ///     Enriches exception with additional system-wise data
        /// </summary>
        protected EcsSystemException CreateException(Exception inner, ReportHint hint = ReportHint.None) =>
            CreateException(inner, hint, false);

        private EcsSystemException CreateException(Exception inner, ReportHint hint, bool unhandled) =>
            new (this, inner, new ReportData(GetReportCategory(), hint, sceneInfo), unhandled);
    }
}
