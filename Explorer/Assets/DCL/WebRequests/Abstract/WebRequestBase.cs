namespace DCL.WebRequests
{
    public abstract class WebRequestBase
    {
        protected WebRequestBase(ITypedWebRequest createdFrom)
        {
            CreatedFrom = createdFrom;
        }

        public ITypedWebRequest CreatedFrom { get; }

        public void Dispose()
        {
            CreatedFrom.Dispose();

            OnDispose();
        }

        protected virtual void OnDispose() { }
    }
}
