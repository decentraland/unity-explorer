namespace SceneRunner.Tests.TestUtils
{
    public struct TestSystemInternal
    {
        public bool DisposeCalled;
        public bool InitializeCalled;
        public bool BeforeUpdateCalled;
        public bool UpdateCalled;
        public bool AfterUpdateCalled;

        public void Dispose()
        {
            DisposeCalled = true;
        }

        public void Initialize()
        {
            InitializeCalled = true;
        }

        public void BeforeUpdate(in float t)
        {
            BeforeUpdateCalled = true;
        }

        public void Update(in float t)
        {
            UpdateCalled = true;
        }

        public void AfterUpdate(in float t)
        {
            AfterUpdateCalled = true;
        }
    }
}
