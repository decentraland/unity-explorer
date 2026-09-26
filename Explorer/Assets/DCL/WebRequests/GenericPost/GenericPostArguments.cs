using JetBrains.Annotations;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public struct GenericPostArguments
    {
        public readonly List<IMultipartFormSection>? MultipartFormSections;
        public readonly WWWForm? WWWForm;
        public readonly string PostData;
        public readonly string? ContentType;

        public BufferedStringUploadHandler? UploadHandler;

        public const string JSON = "application/json";
        private const string JSON_UTF8 = "application/json; charset=utf-8";

        private GenericPostArguments(List<IMultipartFormSection> multipartFormSections) : this()
        {
            MultipartFormSections = multipartFormSections;
            PostData = string.Empty;
        }

        private GenericPostArguments(string postData, string contentType) : this()
        {
            PostData = postData;
            ContentType = contentType;
        }

        private GenericPostArguments(WWWForm form) : this()
        {
            PostData = string.Empty;
            WWWForm = form;
        }

        private GenericPostArguments(BufferedStringUploadHandler uploadHandler, string contentType) : this()
        {
            UploadHandler = uploadHandler;
            ContentType = contentType;
            PostData = string.Empty;
        }

        public static GenericPostArguments Empty => new (string.Empty, JSON);

        public static GenericPostArguments CreateStringUploadHandler(BufferedStringUploadHandler uploadHandler, string contentType) =>
            new (uploadHandler, contentType);

        public static GenericPostArguments CreateMultipartForm(List<IMultipartFormSection> multipartFormSections) =>
            new (multipartFormSections);

        public static GenericPostArguments CreateWWWForm(WWWForm form) =>
            new (form);

        public static GenericPostArguments Create(string postData, string contentType) =>
            new (postData, contentType);

        public static GenericPostArguments CreateJson(string postData) =>
            new (postData, JSON);

        public static GenericPostArguments CreateJsonOrDefault(string? postData) =>
            postData == null ? Empty : CreateJson(postData);

        public static GenericPostArguments CreateJsonUtf8(string json)
        {
            return new GenericPostArguments (json, JSON_UTF8);
        }

        public static GenericPostArguments CreateJsonUtf8(object payload)
        {
            return new GenericPostArguments (JsonUtility.ToJson(payload), JSON_UTF8);
        }

        public bool TryConsumeBufferedUploadHandler([MaybeNullWhen(false)] out UploadHandlerRaw uploadHandlerRaw)
        {
            // It's critical to reflect struct changes on the source, as creating an upload handler is a strictly one-time operation
            if (UploadHandler == null)
            {
                uploadHandlerRaw = null;
                return false;
            }

            BufferedStringUploadHandler value = UploadHandler.Value;
            uploadHandlerRaw = value.CreateUploadHandler();
            UploadHandler = value;

            return true;
        }

        public override string ToString() =>
            "GenericPostArguments:"
            + $"\nMultipartFormSections: {MultipartFormSections}"
            + $"\nWWWForm: {WebFormToString(WWWForm)}"
            + $"\nUploadHandler: {UploadHandler?.ToString() ?? ""}"
            + $"\nPostData: {PostData}"
            + $"\nContentType: {ContentType}";

        private static readonly IReadOnlyDictionary<string, string> EMPTY_DICTIONARY = new Dictionary<string, string>();

        private static string WebFormToString(WWWForm? wwwForm)
        {
            if (wwwForm == null) return "Empty web form";

            var sb = new StringBuilder();

            foreach ((string? key, string? value) in wwwForm.headers ?? EMPTY_DICTIONARY)
                sb.Append($"{key} : {value} \t");

            return sb.ToString();
        }
    }
}
