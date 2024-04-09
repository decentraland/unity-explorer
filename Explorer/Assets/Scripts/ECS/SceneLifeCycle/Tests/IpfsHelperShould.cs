using DCL.Ipfs;
using NUnit.Framework;

namespace ECS.SceneLifeCycle.Tests
{
    public class IpfsHelperShould
    {

        public void ParseUrn()
        {
            const string URN = "urn:decentraland:entity:bafkreidnrsziglqgwwdsvtyrdfltiobpymk3png56xieemixlprqbw5gru?=&baseUrl=https://sdk-team-cdn.decentraland.org/ipfs/";

            IpfsPath ipfsPath = IpfsHelper.ParseUrn(URN);

            Assert.AreEqual("bafkreidnrsziglqgwwdsvtyrdfltiobpymk3png56xieemixlprqbw5gru", ipfsPath.EntityId);
            Assert.AreEqual("https://sdk-team-cdn.decentraland.org/ipfs/", ipfsPath.BaseUrl.ToString());
        }
    }
}
