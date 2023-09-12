using System;
using System.Text;

namespace CommunicationData.URLHelpers
{
    public class URLBuilder
    {
        private readonly StringBuilder stringBuilder;

        private bool endsWithSlash => stringBuilder[^1] == '/';

        private byte parametersCount;

        public URLDomain? URLDomain { get; private set; }
        public URLPath? URLPath { get; private set; }

        /// <summary>
        ///     Set the full domain of the URL, must be called first
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public URLBuilder AppendDomain(in URLDomain domain)
        {
            if (URLDomain != null)
                throw new InvalidOperationException("Domain already set");

            URLDomain = domain;
            AppendWithoutQuestionMark(domain.Value);

            return this;
        }

        /// <summary>
        ///     Append a subdirectory to the URL, handles slashes
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public URLBuilder AppendSubDirectory(in URLSubdirectory subdirectory)
        {
            if (URLDomain == null)
                throw new InvalidOperationException("Subdirectory should be set after domain");

            if (URLPath != null)
                throw new InvalidOperationException("Subdirectory should be set before path");

            if (!endsWithSlash && !subdirectory.Value.StartsWith("/"))
                stringBuilder.Append("/");

            stringBuilder.Append(subdirectory.Value);
            return this;
        }

        /// <summary>
        ///     Append the final part of the URL, handles slashes
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public URLBuilder AppendPath(in URLPath path)
        {
            if (URLDomain == null)
                throw new InvalidOperationException("Path should be set after domain");

            if (URLPath != null)
                throw new InvalidOperationException("Path already set");

            if (!endsWithSlash && !path.Value.StartsWith("/"))
                stringBuilder.Append("/");

            AppendWithoutQuestionMark(path.Value);
            URLPath = path;
            return this;
        }

        private void AppendWithoutQuestionMark(string value)
        {
            if (value.EndsWith("&"))
                stringBuilder.Append(value.AsSpan()[..^1]);
            else
                stringBuilder.Append(value);
        }

        /// <summary>
        ///     Append a parameter to the URL, handles question mark and ampersand cases
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public URLBuilder AppendParameter(in URLParameter parameter)
        {
            if (URLDomain == null)
                throw new InvalidOperationException("Parameter should be set after domain");

            if (parametersCount == 0)
            {
                // Remove a trailing slash
                if (endsWithSlash)
                    stringBuilder.Remove(stringBuilder.Length - 1, 1);

                stringBuilder.Append("?");
            }

            if (parametersCount > 0)
                stringBuilder.Append("&");

            stringBuilder.Append(parameter.Name);
            stringBuilder.Append("=");
            stringBuilder.Append(parameter.Value);

            parametersCount++;
            return this;
        }

        public URLAddress Build() =>
            new (ToString());

        public override string ToString() =>
            stringBuilder.ToString();

        /// <summary>
        ///     Reset the instance so the underlying StringBuilder can be reused
        /// </summary>
        public void Clear()
        {
            stringBuilder.Clear();
            URLDomain = null;
            URLPath = null;
        }

        /// <summary>
        ///     Combines a domain and a subdirectory into a new domain.
        ///     Handles slashes.
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="subdirectory"></param>
        /// <returns></returns>
        public static URLDomain Combine(in URLDomain domain, in URLSubdirectory subdirectory) =>
            new (Combine(domain.Value, subdirectory.Value));

        /// <summary>
        ///     Combines a domain and a path into a new final address.
        ///     Handles slashes.
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static URLAddress Combine(in URLDomain domain, in URLPath path) =>
            new (Combine(domain.Value, path.Value));

        private static string Combine(string domainValue, string appendValue)
        {
            bool domainEndsWithSlash = domainValue.EndsWith('/');
            bool subdirectoryStartsWithSlash = appendValue.StartsWith('/');

            if (domainEndsWithSlash && subdirectoryStartsWithSlash)
            {
                // Remove the slash from the subdirectory
                Span<char> finalSpan = stackalloc char[domainValue.Length + appendValue.Length - 1];
                domainValue.AsSpan().CopyTo(finalSpan);
                appendValue.AsSpan()[1..].CopyTo(finalSpan[domainValue.Length..]);
                return new string(finalSpan);
            }

            if (domainEndsWithSlash || subdirectoryStartsWithSlash)
                return domainValue + appendValue;

            return domainValue + "/" + appendValue;
        }
    }
}
