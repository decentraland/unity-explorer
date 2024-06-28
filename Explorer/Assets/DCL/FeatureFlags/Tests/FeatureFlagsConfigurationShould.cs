using Newtonsoft.Json;
using NUnit.Framework;
using System;

namespace DCL.FeatureFlags.Tests
{
    public class FeatureFlagsConfigurationShould
    {
        private FeatureFlagsConfiguration configuration;

        [SetUp]
        public void SetUp()
        {
            const string JSON = @"{""flags"":{""enabled-ff-1"":true,""enabled-ff-2"":true,""disabled-ff-1"":false},""variants"":{""enabled-ff-1"":{""name"":""disabled"",""enabled"":false},""enabled-ff-2"":{""name"":""disabled"",""enabled"":false},""text-ff"":{""name"":""energy"",""payload"":{""type"":""string"",""value"":""100""},""enabled"":true},""json-ff"":{""name"":""users"",""payload"":{""type"":""json"",""value"":""{\""mode\"": 0, \""allowList\"": [\""0x4ddr3551\"",\""0x4ddr3552\""]}""},""enabled"":true},""csv-ff"":{""name"":""names"",""payload"":{""type"":""csv"",""value"":""pepito,juancito,menganito""},""enabled"":true}}}";
            FeatureFlagsResultDto dto = JsonConvert.DeserializeObject<FeatureFlagsResultDto>(JSON);
            configuration = new FeatureFlagsConfiguration(dto);
        }

        [TestCase("enabled-ff-1")]
        [TestCase("enabled-ff-2")]
        public void GetFeatureFlagEnabled(string id)
        {
            Assert.IsTrue(configuration.IsEnabled(id));
        }

        [TestCase("text-ff", "energy")]
        [TestCase("json-ff", "users")]
        [TestCase("csv-ff", "names")]
        public void GetFeatureFlagVariantEnabled(string id, string variant)
        {
            Assert.IsTrue(configuration.IsEnabled(id, variant));
        }

        [TestCase("non-existing-feature")]
        [TestCase("disabled-ff-1")]
        public void GetFeatureFlagDisabled(string id)
        {
            Assert.IsFalse(configuration.IsEnabled(id));
        }

        [TestCase("text-ff", "bleh")]
        [TestCase("json-ff", "cha")]
        [TestCase("csv-ff", "non-existent")]
        public void GetFeatureFlagVariantDisabled(string id, string variant)
        {
            Assert.IsFalse(configuration.IsEnabled(id, variant));
        }

        [Test]
        public void DontGetPayloadWhenDoesNotExist()
        {
            bool get = configuration.TryGetPayload("non-existing-feature", "any-variant", out var payload);
            Assert.IsFalse(get);
            Assert.IsNull(payload.value);
            Assert.IsNull(payload.type);
        }

        [Test]
        public void GetTextPayload()
        {
            bool get = configuration.TryGetTextPayload("text-ff", "energy", out string? text);
            Assert.IsTrue(get);
            Assert.AreEqual("100", text);
        }

        [Test]
        public void GetJsonPayload()
        {
            bool get = configuration.TryGetJsonPayload("json-ff", "users",
                out UsersAllowedToCreateChannelsDto payload);
            Assert.IsTrue(get);
            Assert.AreEqual(0, payload.mode);
            Assert.AreEqual(2, payload.allowList.Length);
            Assert.AreEqual("0x4ddr3551", payload.allowList[0]);
            Assert.AreEqual("0x4ddr3552", payload.allowList[1]);
        }

        [Test]
        public void GetCsvPayload()
        {
            bool get = configuration.TryGetCsvPayload("csv-ff", "names",
                out var csv);
            Assert.IsTrue(get);
            Assert.AreEqual(1, csv!.Count);
            Assert.AreEqual(3, csv[0]!.Count);
            Assert.AreEqual("pepito", csv[0][0]);
            Assert.AreEqual("juancito", csv[0][1]);
            Assert.AreEqual("menganito", csv[0][2]);
        }

        [Serializable]
        private struct UsersAllowedToCreateChannelsDto
        {
            public int mode;
            public string[] allowList;
        }
    }
}
