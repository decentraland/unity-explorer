using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;

namespace DCL.Backpack.Gifting.Services.SnapshotEquipped
{
    public class AvatarEquippedStatusProvider : IAvatarEquippedStatusProvider
    {
        private readonly SelfProfile selfProfile;
        private readonly HashSet<string> equippedUrns = new ();

        public AvatarEquippedStatusProvider(SelfProfile selfProfile)
        {
            this.selfProfile = selfProfile;
        }

        public async UniTask InitializeAsync(CancellationToken ct)
        {
            equippedUrns.Clear();

            if (!(await selfProfile.ProfileAsync(ct)).IsOk(out Profile? profile) || ct.IsCancellationRequested) return;

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

        public void LogEquippedStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Currently Equipped (Snapshot) ===");
            foreach (string? urn in equippedUrns) sb.AppendLine(urn);
            ReportHub.Log(ReportCategory.GIFTING, sb.ToString());
        }
    }
}