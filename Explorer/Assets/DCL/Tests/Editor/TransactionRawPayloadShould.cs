using DCL.Web3;
using DCL.Web3.Authenticators;
using NUnit.Framework;

namespace DCL.EditModeTests
{
    [TestFixture]
    public class TransactionRawPayloadShould
    {
        private const string CONTRACT = "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4";

        [Test]
        public void RenderTransactionFields()
        {
            var request = new TransactionConfirmationRequest
            {
                Method = "eth_sendTransaction",
                ChainId = 137,
                NetworkName = "Polygon",
                To = CONTRACT,
                Value = "0x2386f26fc10000",
                Data = "0x23b872dd",
            };

            string payload = TransactionRawPayload.Format(request);

            Assert.AreEqual("Method\neth_sendTransaction\n\nNetwork\nPolygon (137)\n\nTo\n" + CONTRACT
                            + "\n\nValue\n0x2386f26fc10000\n\nData\n0x23b872dd", payload);
        }

        [Test]
        public void StateAbsentValueAndData()
        {
            var request = new TransactionConfirmationRequest
            {
                Method = "eth_sendTransaction",
                ChainId = 137,
                NetworkName = "Polygon",
                To = CONTRACT,
            };

            string payload = TransactionRawPayload.Format(request);

            StringAssert.Contains("Value\n0x0", payload);
            StringAssert.Contains("Data\n0x", payload);
        }

        [Test]
        public void FallBackToChainIdWhenNetworkIsUnnamed()
        {
            var request = new TransactionConfirmationRequest
            {
                Method = "eth_sendTransaction",
                ChainId = 11155111,
                To = CONTRACT,
            };

            StringAssert.Contains("Network\n11155111", TransactionRawPayload.Format(request));
        }

        [Test]
        public void IndentTypedDataAndOmitTransactionFields()
        {
            var request = new TransactionConfirmationRequest
            {
                Method = "eth_signTypedData_v4",
                ChainId = 137,
                NetworkName = "Polygon",
                TypedData = "{\"primaryType\":\"Permit\",\"message\":{\"nonce\":0}}",
            };

            string payload = TransactionRawPayload.Format(request);

            StringAssert.Contains("\"primaryType\": \"Permit\"", payload);
            StringAssert.DoesNotContain("Value", payload);
            StringAssert.DoesNotContain("Data\n", payload);
        }

        [Test]
        public void KeepUnparseableTypedDataVerbatim()
        {
            var request = new TransactionConfirmationRequest
            {
                Method = "eth_signTypedData_v4",
                ChainId = 137,
                TypedData = "not json",
            };

            StringAssert.Contains("Typed data\nnot json", TransactionRawPayload.Format(request));
        }
    }
}
