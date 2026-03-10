using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
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

            if (!wearablesDictionary.ContainsKey(wearable.GetCategory()))
                wearablesDictionary[wearable.GetCategory()] = new List<URN>();

            wearablesDictionary[wearable.GetCategory()].Add(wearable.GetUrn());
        }
    }
}
