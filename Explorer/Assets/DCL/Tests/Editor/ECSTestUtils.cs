using CommunicationData.URLHelpers;
using NSubstitute;
using SceneRunner.Scene;

namespace DCL.Tests.Editor
{
    public abstract class ECSTestUtils
    {
        public static ISceneData SceneDataSub()
        {
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.TryGetContentUrl(Arg.Any<string>(), out Arg.Any<URLAddress>())
                     .Returns(args =>
                      {
                          args[1] = URLAddress.FromString(args.ArgAt<string>(0));
                          return true;
                      });
            return sceneData;
        }
    }
}
