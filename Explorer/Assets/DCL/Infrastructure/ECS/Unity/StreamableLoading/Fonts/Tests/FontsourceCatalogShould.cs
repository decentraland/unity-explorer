using NUnit.Framework;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class FontsourceCatalogShould
    {
        private const string VERSION = "5.3.0";

        [TestCase("Roboto", "roboto")]
        [TestCase("Playfair Display", "playfair-display")]
        [TestCase("  Noto   Sans JP ", "noto-sans-jp")]
        [TestCase("Open-Sans", "open-sans")]
        public void MapFamilyNameToFontsourceId(string familyName, string expectedId)
        {
            string? id = FontsourceCatalog.ToFamilyId(familyName);

            Assert.That(id, Is.EqualTo(expectedId));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Font$Name")]
        [TestCase("Über Font")]
        [TestCase("fonts.ttf")]
        public void RejectNamesThatAreNotFamilyNames(string familyName)
        {
            string? id = FontsourceCatalog.ToFamilyId(familyName);

            Assert.That(id, Is.Null);
        }

        [Test]
        public void PointTheRecordUrlAtTheFamilyId()
        {
            string url = FontsourceCatalog.ApiUrl("playfair-display");

            Assert.That(url, Is.EqualTo(FontsourceCatalog.API_BASE_URL + "playfair-display"));
        }

        [Test]
        public void PickTheRegularWeightAndPinTheVersion()
        {
            FontsourceFamilyRecord record = Record(("400", "normal", "latin"), ("700", "normal", "latin"));

            string? regular = VariantUrl(record, FontVariant.Regular);
            string? bold = VariantUrl(record, FontVariant.Bold);

            Assert.That(regular, Is.EqualTo(Url("400", "normal", "latin", VERSION)));
            Assert.That(bold, Is.EqualTo(Url("700", "normal", "latin", VERSION)));
        }

        [Test]
        public void FallBackToTheClosestWeights()
        {
            FontsourceFamilyRecord record = Record(("300", "normal", "latin"), ("600", "normal", "latin"), ("300", "italic", "latin"));

            string? regular = VariantUrl(record, FontVariant.Regular);
            string? bold = VariantUrl(record, FontVariant.Bold);
            string? italic = VariantUrl(record, FontVariant.Italic);
            string? boldItalic = VariantUrl(record, FontVariant.BoldItalic);

            Assert.That(regular, Is.EqualTo(Url("300", "normal", "latin", VERSION)));
            Assert.That(bold, Is.EqualTo(Url("600", "normal", "latin", VERSION)));
            Assert.That(italic, Is.EqualTo(Url("300", "italic", "latin", VERSION)));
            Assert.That(boldItalic, Is.Null);
        }

        [Test]
        public void PreferLatinButAcceptTheFirstListedSubset()
        {
            FontsourceFamilyRecord record = Record(("400", "normal", "cyrillic"), ("400", "normal", "greek"));
            record.subsets = new List<string> { "greek", "cyrillic" };

            string? regular = VariantUrl(record, FontVariant.Regular);

            Assert.That(regular, Is.EqualTo(Url("400", "normal", "greek", VERSION)));
        }

        [Test]
        public void KeepTheUrlWhenTheRecordHasNoVersion()
        {
            FontsourceFamilyRecord record = Record(("400", "normal", "latin"));
            record.npmVersion = null;

            string? regular = VariantUrl(record, FontVariant.Regular);

            Assert.That(regular, Is.EqualTo(Url("400", "normal", "latin", "latest")));
        }

        [Test]
        public void FindNoUrlWhenTheVariantHasNoTtf()
        {
            FontsourceFamilyRecord record = Record(("400", "normal", "latin"));
            record.variants!["400"]["normal"]["latin"].url!.ttf = null;

            string? regular = VariantUrl(record, FontVariant.Regular);

            Assert.That(regular, Is.Null);
        }

        [Test]
        public void FindNoUrlInAnEmptyRecord()
        {
            string? regular = VariantUrl(new FontsourceFamilyRecord(), FontVariant.Regular);

            Assert.That(regular, Is.Null);
        }

        [TestCase("5.0.0/../../other-package")]
        [TestCase("5.0.0?query")]
        [TestCase("5.0.0#fragment")]
        [TestCase("5.0.0%2f")]
        [TestCase("5.0.0\\other")]
        [TestCase("5.0.0\n")]
        public void RejectUnsafePackageVersions(string version)
        {
            FontsourceFamilyRecord record = Record(("400", "normal", "latin"));
            record.npmVersion = version;

            Assert.That(VariantUrl(record, FontVariant.Regular), Is.Null);
        }

        [TestCase("5.0.0")]
        [TestCase("5.0.0-beta.1+build.42")]
        public void AcceptReleaseAndPrereleaseVersions(string version)
        {
            FontsourceFamilyRecord record = Record(("400", "normal", "latin"));
            record.npmVersion = version;

            Assert.That(VariantUrl(record, FontVariant.Regular), Is.EqualTo(Url("400", "normal", "latin", version)));
        }

        private static string? VariantUrl(FontsourceFamilyRecord record, FontVariant variant) =>
            FontsourceCatalog.TryGetVariantUrl(record, variant, out string url) ? url : null;

        private static string Url(string weight, string style, string subset, string version) =>
            $"https://cdn.jsdelivr.net/fontsource/fonts/test@{version}/{subset}-{weight}-{style}.ttf";

        private static FontsourceFamilyRecord Record(params (string weight, string style, string subset)[] faces)
        {
            var record = new FontsourceFamilyRecord
            {
                npmVersion = VERSION,
                subsets = new List<string> { "latin" },
                variants = new Dictionary<string, Dictionary<string, Dictionary<string, FontsourceFamilyRecord.Files>>>(),
            };

            foreach ((string weight, string style, string subset) in faces)
            {
                if (!record.variants.TryGetValue(weight, out Dictionary<string, Dictionary<string, FontsourceFamilyRecord.Files>> styles))
                    record.variants[weight] = styles = new Dictionary<string, Dictionary<string, FontsourceFamilyRecord.Files>>();

                if (!styles.TryGetValue(style, out Dictionary<string, FontsourceFamilyRecord.Files> subsets))
                    styles[style] = subsets = new Dictionary<string, FontsourceFamilyRecord.Files>();

                subsets[subset] = new FontsourceFamilyRecord.Files { url = new FontsourceFamilyRecord.Urls { ttf = Url(weight, style, subset, "latest") } };
            }

            return record;
        }
    }
}
