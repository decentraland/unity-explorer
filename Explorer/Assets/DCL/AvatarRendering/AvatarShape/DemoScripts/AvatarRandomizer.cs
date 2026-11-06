using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using Runtime.Wearables;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.DemoScripts
{
    public class AvatarRandomizer
    {
        public string BodyShape;
        public Dictionary<string, List<URN>> wearablesDictionary;

        public AvatarRandomizer(string bodyShape)
        {
            BodyShape = bodyShape;
            wearablesDictionary = new Dictionary<string, List<URN>>();
        }

        public URN[] GetRandomAvatarWearables()
        {
            var randomWearables = new URN[wearablesDictionary.Count];
            var i = 0;

            foreach (List<URN>? wearableByCategory in wearablesDictionary.Values)
            {
                randomWearables[i] = wearableByCategory[Random.Range(0, wearableByCategory.Count)];
                i++;
            }

            return randomWearables;
        }

        public void AddWearable(ITrimmedWearable wearable)
        {
            if (wearable.GetCategory().Equals(WearableCategories.Categories.BODY_SHAPE))
                return;

            if (!wearable.IsCompatibleWithBodyShape(BodyShape))
                return;

            string category = wearable.GetCategory();

            if (!wearablesDictionary.TryGetValue(category, out List<URN>? urns))
            {
                urns = new List<URN>();
                wearablesDictionary[category] = urns;
            }

            urns.Add(wearable.GetUrn());
        }
    }
}
