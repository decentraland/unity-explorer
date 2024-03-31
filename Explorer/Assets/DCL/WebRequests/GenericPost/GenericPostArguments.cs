using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GenericPostArguments
    {
        public readonly List<IMultipartFormSection>? MultipartFormSections;
        public readonly WWWForm? WWWForm;
        public readonly string PostData;
        public readonly string ContentType;

        private GenericPostArguments(List<IMultipartFormSection> multipartFormSections)
        {
            MultipartFormSections = multipartFormSections;
            PostData = string.Empty;
            ContentType = null;
            WWWForm = null;
        }

        private GenericPostArguments(string postData, string contentType)
        {
            MultipartFormSections = null;
            PostData = postData;
            ContentType = contentType;
            WWWForm = null;
        }

        private GenericPostArguments(WWWForm form)
        {
            MultipartFormSections = null;
            PostData = string.Empty;
            ContentType = null;
            WWWForm = form;
        }

        public static GenericPostArguments Empty => new (string.Empty, "application/json");

        public static GenericPostArguments CreateMultipartForm(List<IMultipartFormSection> multipartFormSections) =>
            new (multipartFormSections);

        public static GenericPostArguments CreateWWWForm(WWWForm form) =>
            new (form);

        public static GenericPostArguments Create(string postData, string contentType) =>
            new (postData, contentType);

        public static GenericPostArguments CreateJson(string postData) =>
            new (postData, "application/json");
    }
}
