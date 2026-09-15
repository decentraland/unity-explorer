using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Shop
{

    public class ShopCreatorNameCache
    {
        private readonly ProfileRepositoryWrapper profiles;
        private readonly Dictionary<string, string> namesByAddress = new (StringComparer.OrdinalIgnoreCase);
        private readonly List<string> pending = new ();

        public ShopCreatorNameCache(ProfileRepositoryWrapper profiles)
        {
            this.profiles = profiles;
        }

        public string GetDisplayName(string address) =>
            namesByAddress.TryGetValue(address, out string? name) ? name : ShortenWallet(address);

        public async UniTask<bool> ResolveAsync(IReadOnlyList<string> addresses, CancellationToken ct)
        {
            pending.Clear();

            foreach (string address in addresses)
            {
                if (string.IsNullOrEmpty(address) || namesByAddress.ContainsKey(address) || pending.Contains(address))
                    continue;

                pending.Add(address);
            }

            if (pending.Count == 0)
                return false;

            string[] batch = pending.ToArray();
            pending.Clear();

            Result<List<Profile.CompactInfo>> result = await profiles.GetProfilesAsync(batch, ct).SuppressToResultAsync(ReportCategory.UI);

            if (ct.IsCancellationRequested)
                return false;

            var changed = false;

            if (result.Success)
            {
                foreach (Profile.CompactInfo profile in result.Value)
                {
                    string address = profile.Address;

                    if (string.IsNullOrEmpty(profile.ValidatedName))
                        continue;

                    namesByAddress[address] = profile.ValidatedName;
                    changed = true;
                }
            }

            foreach (string address in batch)
            {
                if (!namesByAddress.ContainsKey(address))
                    namesByAddress[address] = ShortenWallet(address);
            }

            return changed;
        }

        public static string ShortenWallet(string wallet) =>
            wallet.Length > 10 ? string.Concat(wallet[..6], "...", wallet[^4..]) : wallet;
    }
}
