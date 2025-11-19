using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Profiles.Self;

namespace DCL.Backpack.Gifting.Services.SnapshotEquipped
{
    public class SnapshotEquippedService : ISnapshotEquippedService
    {
        private readonly ISelfProfile selfProfile;
        private readonly HashSet<string> equippedUrns = new ();

        public SnapshotEquippedService(ISelfProfile selfProfile)
        {
            this.selfProfile = selfProfile;
        }

        public async UniTask InitializeAsync(CancellationToken ct)
        {
            equippedUrns.Clear();

            var profile = await selfProfile.ProfileAsync(ct);
            if (ct.IsCancellationRequested || profile == null) return;

            foreach (var w in profile.Avatar.Wearables)
                if (!string.IsNullOrEmpty(w))
                    equippedUrns.Add(w);

            foreach (var e in profile.Avatar.Emotes)
                if (!string.IsNullOrEmpty(e))
                    equippedUrns.Add(e);
        }

        public bool IsEquipped(string urn)
        {
            if (equippedUrns.Contains(urn)) return true;

            foreach (string? equipped in equippedUrns)
            {
                if (equipped.StartsWith(urn) && equipped.Length > urn.Length && equipped[urn.Length] == ':')
                    return true;
            }

            return false;
        }
    }
}