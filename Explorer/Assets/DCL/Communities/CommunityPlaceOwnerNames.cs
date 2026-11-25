using Cysharp.Threading.Tasks;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Communities
{
    internal class CommunityPlaceOwnerNames
    {
        private readonly IProfileRepository profiles;
        private readonly Dictionary<string, string> names = new (StringComparer.OrdinalIgnoreCase);

        public CommunityPlaceOwnerNames(IProfileRepository profiles)
        {
            this.profiles = profiles;
        }

        public string GetName(string? owner) =>
            owner != null && names.TryGetValue(owner, out string name) ? name : string.Empty;

        public void Clear() => names.Clear();

        public async UniTask LoadAsync(IReadOnlyList<PlacesData.PlaceInfo> places, CancellationToken ct)
        {
            HashSet<string>? missing = null;
            foreach (PlacesData.PlaceInfo place in places)
            {
                if (!string.IsNullOrWhiteSpace(place.owner) && !names.ContainsKey(place.owner))
                    (missing ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(place.owner);
            }

            if (missing == null)
                return;

            var result = await profiles.GetCompactWithFailuresAsync(missing, ct);
            if (ct.IsCancellationRequested)
                return;

            foreach (Profile.CompactInfo profile in result.Profiles)
                names[profile.UserId.Value] = profile.Name;

            if (result.HadErrors && result.Profiles.Count == 0)
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification("There was an error getting owners names. Please try again."));
        }
    }
}
