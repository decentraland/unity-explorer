using Arch.SystemGroups;
using UnityEngine;

namespace ECS.Groups
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class SyncedInitializationFixedUpdateThrottledGroup : FixedUpdateThrottledGroup { }

    /// <summary>
    ///     Throttles execution of the systems not letting them pass more than once per FixedUpdate
    ///     regardless the group they belong to on their own
    /// </summary>
    public abstract class FixedUpdateThrottledGroup : CustomGroupBase<float>
    {
        private float processedFixedTime = -1;
        private bool allowed;

        public override void Initialize()
        {
            InitializeInternal();
        }

        public override void Dispose() { }

        public override void BeforeUpdate(in float t, bool throttle)
        {
            if (Time.fixedTime > processedFixedTime)
            {
                allowed = true;
                processedFixedTime = Time.fixedTime;
            }
            else
                allowed = false;

            if (allowed)
                BeforeUpdateInternal(in t, throttle);
        }

        public override void Update(in float t, bool throttle)
        {
            if (allowed)
                UpdateInternal(in t, throttle);
        }

        public override void AfterUpdate(in float t, bool throttle)
        {
            if (allowed)
                AfterUpdateInternal(in t, throttle);
        }
    }
}
