using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;

namespace Localization.Test
{
    public class LocalizationSystemShould
    {
        private static KeyValuePair<string, string>[] values = {
            new ("en", "TestStringEng"),
            new ("it", "TestStringIta"),
            new ("es", "TestStringEs"),
        };

        [SetUp]
        public void SetUp()
        {
        }

        [Test]
        [RequiresPlayMode]
        public void TranslateFromLocalesTable([ValueSource(nameof(values))] KeyValuePair<string, string> langTranslationMapping)
        {
            var locales = LocalizationSettings.AvailableLocales.GetLocale(langTranslationMapping.Key);
            string localizedString = LocalizationSettings.StringDatabase.GetLocalizedString("Tests Localization Table", "TestString", locales);
            Assert.AreEqual(langTranslationMapping.Value, localizedString);
        }
    }
}
