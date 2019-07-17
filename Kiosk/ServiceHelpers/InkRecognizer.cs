using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Input.Inking;

namespace ServiceHelpers
{
    public class InkRecognizer
    {
        string inkRecognitionUrl;
        HttpClient httpClient;

        public IDictionary<uint, InkStroke> StrokeMap { get; set; }
        public string LanguageCode { get; set; } = "en-US";
        public bool IsRecognizing { get; set; } = false;

        public InkRecognizer(string subscriptionKey, string endpoint, string inkRecognitionUrl)
        {
            httpClient = new HttpClient() { BaseAddress = new Uri(endpoint) };
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            this.inkRecognitionUrl = inkRecognitionUrl;

            StrokeMap = new Dictionary<uint, InkStroke>();
        }

        public void AddStroke(InkStroke stroke)
        {
            StrokeMap[stroke.Id] = stroke;
        }

        public void RemoveStroke(uint strokeId)
        {
            StrokeMap.Remove(strokeId);
        }

        public void SetLanguage(string language)
        {
            switch (language)
            {
                case "Chinese (Simplified)":
                    LanguageCode = "zh-CN";
                    break;
                case "Chinese (Traditional)":
                    LanguageCode = "zh-TW";
                    break;
                case "English (US)":
                    LanguageCode = "en-US";
                    break;
                case "English (UK)":
                    LanguageCode = "en-GB";
                    break;
                case "French":
                    LanguageCode = "fr-FR";
                    break;
                case "German":
                    LanguageCode = "de-DE";
                    break;
                case "Italian":
                    LanguageCode = "it-IT";
                    break;
                case "Japanese":
                    LanguageCode = "ja-JP";
                    break;
                case "Korean":
                    LanguageCode = "ko-KR";
                    break;
                case "Spanish":
                    LanguageCode = "es-ES";
                    break;                    
            }
        }

        public JsonObject ConvertInkToJson()
        {
            const float dipsPerMm = 96 / 25.4f;
            var payload = new JsonObject();
            var strokesArray = new JsonArray();

            foreach (var stroke in StrokeMap.Values)
            {
                var jStroke = new JsonObject();
                var pointsCollection = stroke.GetInkPoints();
                var transform = stroke.PointTransform;

                jStroke["id"] = JsonValue.CreateNumberValue(stroke.Id);

                if (pointsCollection.Count >= 2)
                {

                    StringBuilder points = new StringBuilder();
                    for (int i = 0; i < pointsCollection.Count; i++)
                    {
                        var transformedPoint = Vector2.Transform(new Vector2((float)pointsCollection[i].Position.X, (float)pointsCollection[i].Position.Y), transform);
                        double x = transformedPoint.X / dipsPerMm;
                        double y = transformedPoint.Y / dipsPerMm;
                        points.Append(x + "," + y);
                        if (i != pointsCollection.Count - 1)
                        {
                            points.Append(",");
                        }
                    }

                    jStroke["points"] = JsonValue.CreateStringValue(points.ToString());
                    strokesArray.Add(jStroke);
                }
            }
            payload["version"] = JsonValue.CreateNumberValue(1.0);
            payload["language"] = JsonValue.CreateStringValue(LanguageCode);
            payload["strokes"] = strokesArray;

            return payload;
        }

        public async Task<HttpResponseMessage> RecognizeAsync(JsonObject json)
        {
            string payload = json.Stringify();
            var httpContent = new StringContent(payload, Encoding.UTF8, "application/json");
            var httpResponse = await httpClient.PutAsync(inkRecognitionUrl, httpContent);

            return httpResponse;
        }
    }
}
