using Arch.Core;
using UnityEngine;

namespace ECS.Unity.Materials.Components
{
    public struct MaterialComponent
    {
        public enum LifeCycle : byte
        {
            LoadingNotStarted = 0,
            LoadingInProgress = 1,
            LoadingFinished = 2,
            MaterialApplied = 3,
        }

        public MaterialData Data;

        /// <summary>
        ///     The current status of the material loading
        /// </summary>
        public LifeCycle Status;

        /// <summary>
        ///     The final material ready for consumption
        /// </summary>
        public Material Result;

        public EntityReference AlbedoTexPromise;
        public EntityReference EmissiveTexPromise;
        public EntityReference AlphaTexPromise;
        public EntityReference BumpTexPromise;

        public MaterialComponent(MaterialData data)
        {
            AlbedoTexPromise = EntityReference.Null;
            EmissiveTexPromise = EntityReference.Null;
            AlphaTexPromise = EntityReference.Null;
            BumpTexPromise = EntityReference.Null;

            Data = data;
            Status = LifeCycle.LoadingNotStarted;
            Result = null;
        }
    }
}
