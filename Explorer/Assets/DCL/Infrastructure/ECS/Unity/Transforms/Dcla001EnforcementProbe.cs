using Arch.Core;

namespace ECS.Unity.Transforms
{
    /// <summary>
    ///     TEMPORARY - DCLA001 enforcement probe; revert this commit before merge.
    ///     If Unity's compiler applies the severities pinned in Explorer/.editorconfig,
    ///     compiling this file MUST fail the Unity build with error DCLA001. A green
    ///     Unity build with this file present proves the severity plumbing does not
    ///     reach Unity's csc and the corruption-class rules need a ruleset (or an
    ///     Error default severity in the descriptors) instead.
    /// </summary>
    public static class Dcla001EnforcementProbe
    {
        public struct ProbeComponent
        {
            public int Value;
        }

        public static int UseRefAfterStructuralChange(World world, Entity entity)
        {
            ref ProbeComponent component = ref world.Get<ProbeComponent>(entity);
            world.Remove<ProbeComponent>(entity);
            return component.Value;
        }
    }
}
