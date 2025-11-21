using DCL.Optimization.PerformanceBudgeting;
using System.Diagnostics.CodeAnalysis;

namespace DCL.Profiles
{
    public interface IProfileCache
    {
        public ProfileTier? Get(string id);

        public Profile.CompactInfo? GetCompact(string id) =>
            Get(id).ToCompact();

        public bool TryGet(string id, ProfileTier.Kind tier, out ProfileTier profile);

        public bool TryGet(string id, [MaybeNullWhen(false)] out Profile profile)
        {
            if (TryGet(id, ProfileTier.Kind.Full, out ProfileTier tiered) && tiered.IsFull(out profile!))
                return true;

            profile = null;
            return false;
        }

        public bool TryGetCompact(string id, [MaybeNullWhen(false)] out Profile.CompactInfo profile)
        {
            if (TryGet(id, ProfileTier.Kind.Full, out ProfileTier tiered) && tiered.IsCompact(out profile!))
                return true;

            profile = default(Profile.CompactInfo);
            return false;
        }

        public ProfileTier? GetByUserName(string userName);

        public void Set(string id, ProfileTier profile);

        void Remove(string id);

        void Unload(IPerformanceBudget concurrentBudgetProvider, int maxAmount);
    }
}
