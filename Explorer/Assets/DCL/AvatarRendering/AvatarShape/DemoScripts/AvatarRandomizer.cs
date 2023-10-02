using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering
{
    public class AvatarRandomizer
    {
        public string BodyShape;
        public List<string> upper_body;
        public List<string> lower_body;
        public List<string> feet;
        public List<string> hair;
        public List<string> mouth;
        public List<string> eyes;
        public List<string> eyebros;

        public AvatarRandomizer(string bodyShape)
        {
            BodyShape = bodyShape;
            upper_body = new List<string>();
            lower_body = new List<string>();
            feet = new List<string>();
            hair = new List<string>();
            mouth = new List<string>();
            eyes = new List<string>();
            eyebros = new List<string>();
        }

        public string[] GetRandomAvatarWearables()
        {
            return new[]
            {
                upper_body[Random.Range(0, upper_body.Count)],
                lower_body[Random.Range(0, lower_body.Count)],
                feet[Random.Range(0, feet.Count)],
                hair[Random.Range(0, hair.Count)],

                //TODO: We still dont have the default asset bundles for this ones
                //We should add them before using them
                //mouth[Random.Range(0, mouth.Count)].metadata.id,
                //eyes[Random.Range(0, eyes.Count)].metadata.id,
                //eyebros[Random.Range(0, eyebros.Count)].metadata.id,
            };
        }

        public void AddWearable(IWearable wearable)
        {
            if (!wearable.IsCompatibleWithBodyShape(BodyShape))
                return;

            switch (wearable.GetCategory())
            {
                case WearablesConstants.Categories.UPPER_BODY:
                    upper_body.Add(wearable.GetUrn());
                    break;
                case WearablesConstants.Categories.LOWER_BODY:
                    lower_body.Add(wearable.GetUrn());
                    break;
                case WearablesConstants.Categories.FEET:
                    feet.Add(wearable.GetUrn());
                    break;
                case WearablesConstants.Categories.HAIR:
                    hair.Add(wearable.GetUrn());
                    break;
                case WearablesConstants.Categories.MOUTH:
                    mouth.Add(wearable.GetUrn());
                    break;
                case WearablesConstants.Categories.EYES:
                    eyes.Add(wearable.GetUrn());
                    break;
                case WearablesConstants.Categories.EYEBROWS:
                    eyebros.Add(wearable.GetUrn());
                    break;
            }
        }
    }
}
