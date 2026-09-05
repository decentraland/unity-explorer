using DCL.Web3;
using Newtonsoft.Json;
using NUnit.Framework;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    // Lives here rather than next to DCL.Web3 (which has no test assembly): the credits purchase flow is the
    // consumer that depends on these wire-format guarantees.
    public class EthApiResponseShould
    {
        [Test]
        public void DeserializeAJsonRpcErrorFrameWithANullId()
        {
            // Servers echo "id": null for requests they could not parse.
            var response = JsonConvert.DeserializeObject<EthApiResponse>(
                @"{""jsonrpc"":""2.0"",""id"":null,""error"":{""code"":-32603,""message"":""execution reverted""}}");

            Assert.AreEqual(0, response.id);
            Assert.IsNotNull(response.error);
            Assert.AreEqual(-32603, response.error!.code);
            Assert.AreEqual("execution reverted", response.error.message);
            Assert.IsNull(response.result);
        }

        [Test]
        public void DeserializeAJsonRpcErrorFrameWithAnEchoedId()
        {
            var response = JsonConvert.DeserializeObject<EthApiResponse>(
                @"{""jsonrpc"":""2.0"",""id"":42,""error"":{""code"":-32000,""message"":""header not found""}}");

            Assert.AreEqual(42, response.id);
            Assert.IsNotNull(response.error);
            Assert.AreEqual(-32000, response.error!.code);
        }

        [Test]
        public void SerializeWithoutAnErrorMemberWhenThereIsNoError()
        {
            // Responses are forwarded verbatim to SDK scenes; an absent error must keep the payload's shape.
            string json = JsonConvert.SerializeObject(new EthApiResponse
            {
                id = 7,
                jsonrpc = "2.0",
                result = "0x1",
            });

            Assert.AreEqual(@"{""id"":7,""jsonrpc"":""2.0"",""result"":""0x1""}", json);
        }
    }
}
