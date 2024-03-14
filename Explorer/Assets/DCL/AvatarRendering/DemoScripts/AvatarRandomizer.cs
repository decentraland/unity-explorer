using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
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
            URN[] randomWearables = new URN[wearablesDictionary.Count];
            int i = 0;
            foreach (var wearableByCategory in wearablesDictionary.Values)
            {
                randomWearables[i] = wearableByCategory[Random.Range(0, wearableByCategory.Count)];
                i++;
            }
            return randomWearables;
        }

        public void AddWearable(IWearable wearable)
        {
            if (wearable.GetCategory().Equals(WearablesConstants.Categories.BODY_SHAPE))
                return;
            
            if (!wearable.IsCompatibleWithBodyShape(BodyShape))
                return;

            if (!wearablesDictionary.ContainsKey(wearable.GetCategory()))
                wearablesDictionary[wearable.GetCategory()] = new List<URN>();
            wearablesDictionary[wearable.GetCategory()].Add(wearable.GetUrn());
        }
    }
}