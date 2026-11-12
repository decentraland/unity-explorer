using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Shop
{
    /// <summary>
    ///     Display names and profile pictures of item creators, keyed by wallet address. A creator without a profile
    ///     shows the shortened wallet and the default picture.
    /// </summary>
    public class ShopCreatorCache : IDisposable
    {
        private readonly ProfileRepositoryWrapper profiles;
        private readonly Dictionary<string, string> namesByAddress = new (StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ReactiveProperty<ProfileThumbnailViewModel>> thumbnailsByAddress = new (StringComparer.OrdinalIgnoreCase);
        private readonly List<string> pending = new ();
        private readonly CancellationTokenSource thumbnailsCts = new ();

        public ShopCreatorCache(ProfileRepositoryWrapper profiles)
        {
            this.profiles = profiles;
        }

        public void Dispose()
        {
            thumbnailsCts.SafeCancelAndDispose();

            foreach (ReactiveProperty<ProfileThumbnailViewModel> thumbnail in thumbnailsByAddress.Values)
                thumbnail.ClearSubscriptionsList();
        }

        public string GetDisplayName(string address) =>
            namesByAddress.TryGetValue(address, out string? name) ? name : ShortenWallet(address);

        /// <summary>
        ///     The picture of a creator, shared by every card showing it; it loads once the address is resolved.
        /// </summary>
        public IReactiveProperty<ProfileThumbnailViewModel> GetThumbnail(string address)
        {
            if (!thumbnailsByAddress.TryGetValue(address, out ReactiveProperty<ProfileThumbnailViewModel>? thumbnail))
            {
                thumbnail = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default());
                thumbnailsByAddress[address] = thumbnail;
            }

            return thumbnail;
        }

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

                    IReactiveProperty<ProfileThumbnailViewModel> thumbnail = GetThumbnail(address);
                    thumbnail.SetColor(profile.UserNameColor);
                    GetProfileThumbnailCommand.Instance.ExecuteAsync(thumbnail, null, profile, thumbnailsCts.Token).SuppressToResultAsync(ReportCategory.UI).Forget();
                }
            }

            foreach (string address in batch)
            {
                if (namesByAddress.ContainsKey(address))
                    continue;

                namesByAddress[address] = ShortenWallet(address);
                IReactiveProperty<ProfileThumbnailViewModel> thumbnail = GetThumbnail(address);
                thumbnail.UpdateValue(ProfileThumbnailViewModel.Error(thumbnail.Value.ProfileColor));
            }

            return changed;
        }

        public static string ShortenWallet(string wallet) =>
            wallet.Length > 10 ? string.Concat(wallet[..6], "...", wallet[^4..]) : wallet;
    }
}
