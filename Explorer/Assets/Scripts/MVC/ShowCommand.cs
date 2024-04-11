namespace MVC
{
    /// <summary>
    ///     Don't nest it as otherwise it will be referenced by the base type name, it's ugly
    /// </summary>
    /// <typeparam name="TView"></typeparam>
    /// <typeparam name="TInputData"></typeparam>
    public readonly struct ShowCommand<TView, TInputData> where TView: IView
    {
        public readonly TInputData InputData;

        public ShowCommand(TInputData inputData)
        {
            InputData = inputData;
        }
    }
}
