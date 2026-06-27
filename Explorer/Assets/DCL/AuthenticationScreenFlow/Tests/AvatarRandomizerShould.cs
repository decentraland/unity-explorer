using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AuthenticationScreenFlow;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.AuthenticationScreenFlow.Tests
{
    [TestFixture]
    public class AvatarRandomizerShould
    {
        private const string URN_PREFIX = "urn:decentraland:off-chain:base-avatars:";

        private const string UPPER_BODY = "upper_body";
        private const string LOWER_BODY = "lower_body";
        private const string FEET = "feet";
        private const string HAIR = "hair";
        private const string FACIAL_HAIR = "facial_hair";
        private const string HAT = "hat";
        private const string EYEWEAR = "eyewear";
        private const string BODY_SHAPE = "body_shape";

        private AvatarRandomizer randomizer;

        [SetUp]
        public void SetUp()
        {
            randomizer = new AvatarRandomizer();
        }

        [Test]
        public void DetectFemalePrefix()
        {
            URN urn = new URN(URN_PREFIX + "F_uBody_BlackTop");
            Assert.IsTrue(AvatarRandomizer.HasBodyTypePrefix(urn, "f_"));
            Assert.IsFalse(AvatarRandomizer.HasBodyTypePrefix(urn, "m_"));
        }

        [Test]
        public void DetectMalePrefix()
        {
            URN urn = new URN(URN_PREFIX + "M_Hair_Standard_01");
            Assert.IsTrue(AvatarRandomizer.HasBodyTypePrefix(urn, "m_"));
            Assert.IsFalse(AvatarRandomizer.HasBodyTypePrefix(urn, "f_"));
        }

        [Test]
        public void DetectNoPrefixAsNeutral()
        {
            URN urn = new URN(URN_PREFIX + "Sneakers_01");
            Assert.IsFalse(AvatarRandomizer.HasBodyTypePrefix(urn, "f_"));
            Assert.IsFalse(AvatarRandomizer.HasBodyTypePrefix(urn, "m_"));
        }

        [TestCase("F_uBody_BlackTop", "f_")]
        [TestCase("f_uBody_BlackTop", "f_")]
        [TestCase("F_uBody_BlackTop", "F_")]
        public void MatchPrefixCaseInsensitively(string assetName, string prefix)
        {
            URN urn = new URN(URN_PREFIX + assetName);
            Assert.IsTrue(AvatarRandomizer.HasBodyTypePrefix(urn, prefix));
        }

        [Test]
        public void HandleUrnWithNoColon()
        {
            URN urn = new URN("noColonHere");
            Assert.IsFalse(AvatarRandomizer.HasBodyTypePrefix(urn, "f_"));
        }

        [Test]
        public void ExcludeFacialHairFromFemaleCatalog()
        {
            URN beard = new URN(URN_PREFIX + "Beard_01");
            randomizer.AddEntry(FACIAL_HAIR, beard, compatWithMale: true, compatWithFemale: true);

            Assert.IsTrue(randomizer.MaleCatalog!.ContainsKey(FACIAL_HAIR));
            Assert.IsFalse(randomizer.FemaleCatalog!.ContainsKey(FACIAL_HAIR));
        }

        [Test]
        public void ExcludeFemaleDesignedWearablesFromMaleCatalog()
        {
            URN femaleTop = new URN(URN_PREFIX + "F_uBody_BlackTop");
            randomizer.AddEntry(UPPER_BODY, femaleTop, compatWithMale: true, compatWithFemale: true);

            Assert.IsFalse(randomizer.MaleCatalog!.ContainsKey(UPPER_BODY));
            Assert.IsTrue(randomizer.FemaleCatalog!.ContainsKey(UPPER_BODY));
            Assert.AreEqual(1, randomizer.FemaleCatalog[UPPER_BODY].Count);
        }

        [Test]
        public void ExcludeMaleDesignedWearablesFromFemaleCatalog()
        {
            URN maleHair = new URN(URN_PREFIX + "M_Hair_Standard_01");
            randomizer.AddEntry(HAIR, maleHair, compatWithMale: true, compatWithFemale: true);

            Assert.IsTrue(randomizer.MaleCatalog!.ContainsKey(HAIR));
            Assert.IsFalse(randomizer.FemaleCatalog!.ContainsKey(HAIR));
        }

        [Test]
        public void IncludeGenderNeutralWearablesInBothCatalogs()
        {
            URN sneakers = new URN(URN_PREFIX + "Sneakers_01");
            randomizer.AddEntry(FEET, sneakers, compatWithMale: true, compatWithFemale: true);

            Assert.IsTrue(randomizer.MaleCatalog!.ContainsKey(FEET));
            Assert.IsTrue(randomizer.FemaleCatalog!.ContainsKey(FEET));
        }

        [Test]
        public void SelectOnlyFromCorrectCatalog()
        {
            URN maleTop = new URN(URN_PREFIX + "M_uBody_BlueTShirt");
            URN femaleTop = new URN(URN_PREFIX + "F_uBody_BlackTop");
            URN neutralTop = new URN(URN_PREFIX + "uBody_PoloBlackTShirt");

            randomizer.AddEntry(UPPER_BODY, maleTop, compatWithMale: true, compatWithFemale: true);
            randomizer.AddEntry(UPPER_BODY, femaleTop, compatWithMale: true, compatWithFemale: true);
            randomizer.AddEntry(UPPER_BODY, neutralTop, compatWithMale: true, compatWithFemale: true);

            Random.InitState(42);
            HashSet<URN> maleResult = randomizer.SelectRandomWearables(BodyShape.MALE);
            Assert.IsTrue(maleResult.All(w => randomizer.MaleCatalog![UPPER_BODY].Contains(w)));

            Random.InitState(42);
            HashSet<URN> femaleResult = randomizer.SelectRandomWearables(BodyShape.FEMALE);
            Assert.IsTrue(femaleResult.All(w => randomizer.FemaleCatalog![UPPER_BODY].Contains(w)));
        }

        [Test]
        public void AlwaysIncludeMandatoryCategories()
        {
            URN top = new URN(URN_PREFIX + "uBody_PoloBlackTShirt");
            URN pants = new URN(URN_PREFIX + "lBody_CorduroyGreenPants");
            URN shoes = new URN(URN_PREFIX + "Sneakers_01");

            randomizer.AddEntry(UPPER_BODY, top, compatWithMale: true, compatWithFemale: true);
            randomizer.AddEntry(LOWER_BODY, pants, compatWithMale: true, compatWithFemale: true);
            randomizer.AddEntry(FEET, shoes, compatWithMale: true, compatWithFemale: true);

            for (int i = 0; i < 20; i++)
            {
                Random.InitState(i);
                HashSet<URN> result = randomizer.SelectRandomWearables(BodyShape.MALE);
                Assert.AreEqual(3, result.Count, $"Mandatory categories missing on seed {i}");
            }
        }

        [Test]
        public void SometimesSkipOptionalCategories()
        {
            URN top = new URN(URN_PREFIX + "uBody_PoloBlackTShirt");
            URN hat = new URN(URN_PREFIX + "BlueBandana");

            randomizer.AddEntry(UPPER_BODY, top, compatWithMale: true, compatWithFemale: true);
            randomizer.AddEntry(HAT, hat, compatWithMale: true, compatWithFemale: true);

            int hatIncluded = 0;

            for (int i = 0; i < 100; i++)
            {
                Random.InitState(i);
                HashSet<URN> result = randomizer.SelectRandomWearables(BodyShape.MALE);

                Assert.IsTrue(result.Contains(top), "Mandatory category must always appear");

                if (result.Contains(hat))
                    hatIncluded++;
            }

            Assert.Greater(hatIncluded, 0, "Hat should appear at least once in 100 tries");
            Assert.Less(hatIncluded, 100, "Hat should be skipped at least once in 100 tries");
        }

        [Test]
        public void ReportHasCatalogsCorrectly()
        {
            Assert.IsFalse(randomizer.HasCatalogs);

            URN top = new URN(URN_PREFIX + "uBody_PoloBlackTShirt");
            randomizer.AddEntry(UPPER_BODY, top, compatWithMale: true, compatWithFemale: true);
            Assert.IsTrue(randomizer.HasCatalogs);

            randomizer.ClearCatalogs();
            Assert.IsFalse(randomizer.HasCatalogs);
        }

        [Test]
        public void ClassifyOptionalCategories()
        {
            Assert.IsTrue(AvatarRandomizer.IsOptionalCategory(FACIAL_HAIR));
            Assert.IsTrue(AvatarRandomizer.IsOptionalCategory(HAT));
            Assert.IsTrue(AvatarRandomizer.IsOptionalCategory(EYEWEAR));
            Assert.IsFalse(AvatarRandomizer.IsOptionalCategory(UPPER_BODY));
            Assert.IsFalse(AvatarRandomizer.IsOptionalCategory(FEET));
        }

        [Test]
        public void ClassifyFemaleExcludedCategories()
        {
            Assert.IsTrue(AvatarRandomizer.IsFemaleExcludedCategory(FACIAL_HAIR));
            Assert.IsFalse(AvatarRandomizer.IsFemaleExcludedCategory(HAT));
            Assert.IsFalse(AvatarRandomizer.IsFemaleExcludedCategory(UPPER_BODY));
        }
    }
}
