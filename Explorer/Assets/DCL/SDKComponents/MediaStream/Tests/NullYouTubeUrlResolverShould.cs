using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class NullYouTubeUrlResolverShould
    {
        [Test]
        public async Task ReturnNullForAnyUrl()
        {
            var resolver = new NullYouTubeUrlResolver();
            ResolvedYouTubeUrl? result = await resolver.ResolveAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ", CancellationToken.None);
            Assert.That(result, Is.Null);
        }
    }
}
