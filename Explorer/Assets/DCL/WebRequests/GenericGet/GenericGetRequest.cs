namespace DCL.WebRequests
{
    public class GenericGetRequest : TypedWebRequestBase<GenericGetArguments>
    {
        internal GenericGetRequest(RequestEnvelope envelope, GenericGetArguments args, IWebRequestController controller) : base(envelope, args, controller)
        {
        }
    }
}
