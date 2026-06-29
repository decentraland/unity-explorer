using Global.AppArgs;
using NUnit.Framework;
using System.Collections.Generic;

namespace Global.Tests.EditMode
{
    public class ApplicationParametersParserShould
    {
        [Test]
        public void ExtractSigninFromRealProductionFormat()
        {
            // Real auth website format with the 'open' host segment (ADR-288 / auth PR #218).
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters("decentraland://open?signin=abc-123");

            Assert.IsTrue(output.ContainsKey("signin"));
            Assert.AreEqual("abc-123", output["signin"]);
            Assert.IsFalse(output.ContainsKey("open?signin"));
        }

        [Test]
        public void StillParseHostlessRealmAndPosition()
        {
            // Regression: existing host-less format must keep yielding 'realm'/'position'.
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters("decentraland://?realm=http://127.0.0.1:8000&position=100,100");

            Assert.IsTrue(output.ContainsKey("realm"));
            Assert.AreEqual("http://127.0.0.1:8000", output["realm"]);
            Assert.IsTrue(output.ContainsKey("position"));
            Assert.AreEqual("100,100", output["position"]);
        }

        [Test]
        public void StillParseHostlessRealmWithoutLeadingQuestionMark()
        {
            // Regression: the 'decentraland://realm=.../' variant (WinOS) must keep yielding 'realm'.
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters("decentraland://realm=metadyne.dcl.eth/");

            Assert.IsTrue(output.ContainsKey("realm"));
            Assert.AreEqual("metadyne.dcl.eth", output["realm"]);
        }
    }
}
