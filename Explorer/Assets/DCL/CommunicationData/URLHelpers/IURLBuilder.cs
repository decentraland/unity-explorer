using System;

namespace CommunicationData.URLHelpers
{
    public interface IURLBuilder
    {
        string GetResult();

        void Clear();

        Uri Build();

        IURLBuilder AppendDomain(in URLDomain domain);

        IURLBuilder AppendDomainWithReplacedPath(in URLDomain domain, in URLSubdirectory newPath);

        IURLBuilder AppendSubDirectory(in URLSubdirectory subdirectory);

        IURLBuilder AppendParameter(in URLParameter parameter);

        IURLBuilder AppendPath(in URLPath path);

    }
}
