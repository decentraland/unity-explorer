namespace DCL.WebRequests
{
    public abstract class WebRequestBase
    {
        private bool isDisposed;

        protected WebRequestBase(ITypedWebRequest createdFrom)
        {
            CreatedFrom = createdFrom;
        }

        public ITypedWebRequest CreatedFrom { get; }

        internal bool successfullyExecutedByController;

        public string Url => CreatedFrom.Envelope.CommonArguments.URL;

        public void Dispose()
        {
            if (isDisposed)
                return;

            // Can't dispose CreateFrom right-away as it might be re-used from repetitions
            if (successfullyExecutedByController)
                CreatedFrom.Dispose();

            OnDispose();

            isDisposed = true;
        }

        protected virtual void OnDispose() { }

        public override string ToString() =>
            $"{GetType().Name}\nFrom: {CreatedFrom}";
    }
}
