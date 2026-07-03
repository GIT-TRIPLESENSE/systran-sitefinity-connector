using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using Progress.Sitefinity.Translations;
using Telerik.Sitefinity.Translations;

[assembly: TranslationConnector(name: SystranMachineTranslationConnector.ConnectorName,
                                connectorType: typeof(SystranMachineTranslationConnector),
                                title: SystranMachineTranslationConnector.ConnectorTitle,
                                enabled: false,
                                parameters: new string[] {
                                    SystranMachineTranslationConnector.ApiKey,
                                    SystranMachineTranslationConnector.ApiUrl,
                                    SystranMachineTranslationConnector.EnglishToFrenchProfile,
                                    SystranMachineTranslationConnector.EnglishToSpanishProfile,
                                    SystranMachineTranslationConnector.EnglishToItalianProfile,
                                    SystranMachineTranslationConnector.EnglishToGermanProfile,
                                    SystranMachineTranslationConnector.EnglishToPortugueseProfile,
                                    SystranMachineTranslationConnector.EnglishToPolishProfile,
                                    SystranMachineTranslationConnector.EnglishToSlovakProfile
                                })]
namespace Progress.Sitefinity.Translations
{
    public class SystranMachineTranslationConnector : MachineTranslationConnector
    {
        #region Initialization
        protected override void InitializeConnector(NameValueCollection config)
        {
            this.apiKey = config.Get(SystranMachineTranslationConnector.ApiKey);
            if (string.IsNullOrEmpty(this.apiKey))
            {
                throw new ArgumentException(SystranMachineTranslationConnector.NoApiKeyExceptionMessage);
            }

            this.apiUrl = config.Get(SystranMachineTranslationConnector.ApiUrl);
            if (string.IsNullOrEmpty(this.apiUrl))
            {
                this.apiUrl = "https://api-translate.systran.net";
            }

            this.englishToFrenchProfileId = config.Get(SystranMachineTranslationConnector.EnglishToFrenchProfile);
            if (string.IsNullOrEmpty(this.englishToFrenchProfileId))
            {
                this.englishToFrenchProfileId = "bf203401-7e9f-4422-bbf0-c5b988b88d58";
            }

            this.englishToSpanishProfileId = config.Get(SystranMachineTranslationConnector.EnglishToSpanishProfile);
            if (string.IsNullOrEmpty(this.englishToSpanishProfileId))
            {
                this.englishToSpanishProfileId = "23ff1252-d460-46af-99e8-f949706e36f7";
            }

            this.englishToItalianProfileId = config.Get(SystranMachineTranslationConnector.EnglishToItalianProfile);
            if (string.IsNullOrEmpty(this.englishToItalianProfileId))
            {
                this.englishToItalianProfileId = "e3cf2a44-01db-40a5-8a42-0992d1114d01";
            }

            this.englishToGermanProfileId = config.Get(SystranMachineTranslationConnector.EnglishToGermanProfile);
            if (string.IsNullOrEmpty(this.englishToGermanProfileId))
            {
                this.englishToGermanProfileId = "b8f4f0ae-fb79-4f65-9cff-52ac52ef634f";
            }

            this.englishToPortugueseProfileId = config.Get(SystranMachineTranslationConnector.EnglishToPortugueseProfile);
            if (string.IsNullOrEmpty(this.englishToPortugueseProfileId))
            {
                this.englishToPortugueseProfileId = "dc94fed5-e228-4cf7-bebf-ab2c0a0e5ecb";
            }

            this.englishToPolishProfileId = config.Get(SystranMachineTranslationConnector.EnglishToPolishProfile);
            if (string.IsNullOrEmpty(this.englishToPolishProfileId))
            {
                this.englishToPolishProfileId = "e265a495-99b2-469b-b04b-d31ceab3f279";
            }

            this.englishToSlovakProfileId = config.Get(SystranMachineTranslationConnector.EnglishToSlovakProfile);
            if (string.IsNullOrEmpty(this.englishToSlovakProfileId))
            {
                this.englishToSlovakProfileId = "b9ac3e26-25f8-47af-900e-bad2d61368e1";
            }

            this.httpClient = new HttpClient();
        }
        #endregion

        protected override List<string> Translate(List<string> input, ITranslationOptions translationOptions)
        {
            return TranslateTexts(input, translationOptions.SourceLanguage, translationOptions.TargetLanguage);
        }

        private List<string> TranslateTexts(List<string> texts, string sourceLanguage, string targetLanguage)
        {
            var results = new List<string>(texts.Count);

            foreach (var batch in SplitIntoBatches(texts))
            {
                results.AddRange(TranslateBatch(batch, sourceLanguage, targetLanguage));
            }

            return results;
        }

        private IEnumerable<List<string>> SplitIntoBatches(List<string> texts)
        {
            var batch = new List<string>();
            var batchBytes = 0;

            foreach (var text in texts)
            {
                var textBytes = Encoding.UTF8.GetByteCount(text);

                if (batch.Count > 0 && (batch.Count >= MaxBatchItems || batchBytes + textBytes > MaxBatchBytes))
                {
                    yield return batch;
                    batch = new List<string>();
                    batchBytes = 0;
                }

                batch.Add(text);
                batchBytes += textBytes;
            }

            if (batch.Count > 0)
                yield return batch;
        }

        private List<string> TranslateBatch(List<string> texts, string sourceLanguage, string targetLanguage)
        {
            var sourceCode = GetLanguageCode(sourceLanguage);
            var targetCode = GetLanguageCode(targetLanguage);

            if (string.Equals(sourceCode, targetCode, StringComparison.OrdinalIgnoreCase))
                return texts;

            var request = new HttpRequestMessage(HttpMethod.Post, $"{this.apiUrl}/translation/text/translate");
            request.Headers.Add("Authorization", $"Key {this.apiKey}");

            var requestBody = new JObject
            {
                ["input"] = new JArray(texts),
                ["source"] = sourceCode,
                ["target"] = targetCode
            };

            var profileCode = ResolveProfileCode(sourceCode, targetCode);
            if (!string.IsNullOrWhiteSpace(profileCode))
            {
                requestBody["profile"] = profileCode;
            }

            var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
            request.Content = content;

            var response = this.httpClient.SendAsync(request).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var responseJson = JObject.Parse(responseContent);

            var outputs = responseJson["outputs"] as JArray;
            if (outputs == null || outputs.Count != texts.Count)
                throw new InvalidOperationException($"Unexpected Systran API response format. Response: {responseContent}");

            return outputs.Select(o => o["output"]?.ToString() ?? string.Empty).ToList();
        }

        private string ResolveProfileCode(string sourceCode, string targetCode)
        {
            if (!string.Equals(sourceCode, "en", StringComparison.OrdinalIgnoreCase))
                return null;

            var normalizedTargetCode = targetCode?.ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedTargetCode))
                return null;

            switch (normalizedTargetCode)
            {
                case "fr":
                    return this.englishToFrenchProfileId;
                case "es":
                    return this.englishToSpanishProfileId;
                case "it":
                    return this.englishToItalianProfileId;
                case "de":
                    return this.englishToGermanProfileId;
                case "pt":
                    return this.englishToPortugueseProfileId;
                case "pl":
                    return this.englishToPolishProfileId;
                case "sk":
                    return this.englishToSlovakProfileId;
                default:
                    return null;
            }
        }

        private string GetLanguageCode(string cultureCode)
        {
            if (string.IsNullOrEmpty(cultureCode))
                return cultureCode;

            if (cultureCode.Length == 2)
                return cultureCode;

            var dashIndex = cultureCode.IndexOf('-');
            if (dashIndex > 0)
                return cultureCode.Substring(0, dashIndex);

            return cultureCode;
       }

        internal const string ConnectorName = "SystranMachineTranslation";
        internal const string ConnectorTitle = "Systran Machine Translation";
        internal const string ApiKey = "apiKey";
        internal const string ApiUrl = "apiUrl";
        internal const string EnglishToFrenchProfile = "englishToFrenchProfile";
        internal const string EnglishToSpanishProfile = "englishToSpanishProfile";
        internal const string EnglishToItalianProfile = "englishToItalianProfile";
        internal const string EnglishToGermanProfile = "englishToGermanProfile";
        internal const string EnglishToPortugueseProfile = "englishToPortugueseProfile";
        internal const string EnglishToPolishProfile = "englishToPolishProfile";
        internal const string EnglishToSlovakProfile = "englishToSlovakProfile";
        internal const string NoApiKeyExceptionMessage = "No API key configured for Systran translations connector.";        

        private const int MaxBatchItems = 50000;
        private const int MaxBatchBytes = 40 * 1024 * 1024;

        private HttpClient httpClient;
        private string apiKey;
        private string apiUrl;
        private string englishToFrenchProfileId;
        private string englishToSpanishProfileId;
        private string englishToItalianProfileId;
        private string englishToGermanProfileId;
        private string englishToPortugueseProfileId;
        private string englishToPolishProfileId;
        private string englishToSlovakProfileId;
    }

    
}
