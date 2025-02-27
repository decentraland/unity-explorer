using System;

namespace MVC
{
    public abstract class SimpleController<TView, TInputData, TViewData> : IDisposable where TView: SimpleView<TViewData>
    {
        protected TView viewInstance;
        protected TInputData inputData;

        protected SimpleController(TView viewInstance, TInputData inputData)
        {
            this.viewInstance = viewInstance;
            this.inputData = inputData;
        }

        public void UpdateViewWithData(TInputData newInputData)
        {
            this.inputData = newInputData;
            UpdateView();
        }

        public virtual void UpdateView()
        {
            if (viewInstance != null && inputData != null)
            {
                TViewData viewData = ProcessData(inputData);
                viewInstance.Setup(viewData);
            }
        }

        protected abstract TViewData ProcessData(TInputData data);

        public abstract void Dispose();
    }

}
