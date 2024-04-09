using DCL.MapRenderer.Culling;
using NSubstitute;
using NUnit.Framework;

namespace DCL.MapRenderer.Tests.Culling
{
    public class TrackedStateShould
    {
        private MapCullingController.TrackedState<IMapPositionProvider> state;
        private IMapPositionProvider positionProvider;
        private IMapCullingListener<IMapPositionProvider> listener;


        public void SetUp()
        {
            positionProvider = Substitute.For<IMapPositionProvider>();
            listener = Substitute.For<IMapCullingListener<IMapPositionProvider>>();
            state = new MapCullingController.TrackedState<IMapPositionProvider>(positionProvider, listener);
        }




        public void SetCameraFlag(int value)
        {
            state.SetCameraFlag(value);

            Assert.AreEqual(value, state.DirtyCamerasFlag);
        }







        public void SetCameraFlagIndex(int initial, int index, bool value, int expected)
        {
            state.DirtyCamerasFlag = initial;

            state.SetCameraFlag(index, value);

            Assert.AreEqual(expected, state.DirtyCamerasFlag);
        }





        public void ReturnIsDirty(int initial, int index, bool expected)
        {
            state.DirtyCamerasFlag = initial;

            Assert.AreEqual(expected, state.IsCameraDirty(index));
        }




        public void SetVisibleFlag(int value)
        {
            state.SetVisibleFlag(value);

            Assert.AreEqual(value, state.VisibleFlag);
        }







        public void SetVisibleFlagIndex(int initial, int index, bool value, int expected)
        {
            state.VisibleFlag = initial;

            state.SetVisibleFlag(index, value);

            Assert.AreEqual(expected, state.VisibleFlag);
        }






        public void CallListener(int visibleFlag, bool isCulled)
        {
            state.VisibleFlag = visibleFlag;

            state.CallListener();

            if (isCulled)
            {
                listener.Received().OnMapObjectCulled(positionProvider);
                listener.DidNotReceiveWithAnyArgs().OnMapObjectBecameVisible(Arg.Any<IMapPositionProvider>());
            }
            else
            {
                listener.DidNotReceiveWithAnyArgs().OnMapObjectCulled(positionProvider);
                listener.Received().OnMapObjectBecameVisible(Arg.Any<IMapPositionProvider>());
            }
        }
    }
}
