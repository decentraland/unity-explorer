namespace CommunicationData.URLHelpers
{
    public interface IURLBuilder
    {
        string GetResult();

        void Clear();

        URLAddress Build();

        IURLBuilder AppendDomainWithReplacedPath(in URLDomain domain, in URLSubdirectory newPath);

        IURLBuilder AppendSubDirectory(in URLSubdirectory subdirectory);

        IURLBuilder AppendParameter(in URLParameter parameter);

        IURLBuilder AppendPath(in URLPath path);

    }
}
