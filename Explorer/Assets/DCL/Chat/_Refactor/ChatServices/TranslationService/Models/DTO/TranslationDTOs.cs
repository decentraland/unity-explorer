using System;

namespace DCL.Translation.Service.Models
{
    [Serializable]
    public class TranslationRequestBody
    {
        public string q;
        public string source;
        public string target;
        public string format;
    }
    
    [Serializable] 
    public class TranslationRequestBodyBatch
        {
            public string[] q;
            public string source;
            public string target;
            public string format;
        }

    [Serializable]
    public class TranslationApiResponse
    {
        public DetectedLanguageDto detectedLanguage;
        public string translatedText;
    }
    
    [Serializable]
    public class TranslationApiResponseBatch
    {
        public DetectedLanguageDto[] detectedLanguage;
        public string[] translatedText;
    }

    [Serializable]
    public class DetectedLanguageDto
    {
        public float confidence;
        public string language;
    }
}