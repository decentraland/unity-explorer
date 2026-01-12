using Utility;

namespace SceneRuntime
{
    public interface IScriptObject
    {
        object InvokeAsFunction(params object[] args);
        object GetProperty(string name);
        void SetProperty(string name, object value);
        /// <summary>
        /// Sets the value of an indexed script object property.
        /// </summary>
        /// <param name="index">The index of the property to set.</param>
        /// <param name="value">The new property value.</param>
        void SetProperty(int index, object value);
        IScriptObject Invoke(bool asConstructor, params object[] args);
        void SetProperty(int index, IDCLTypedArray<byte> value);
        IDCLTypedArray<byte> InvokeMethod(string subarray, int i, int dataOffset);
    }
}
