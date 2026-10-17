namespace Configurator.Views
{
    public interface IRefreshableView
    {
        public object GetData();
        
        public void SetData(object data);
    }
}