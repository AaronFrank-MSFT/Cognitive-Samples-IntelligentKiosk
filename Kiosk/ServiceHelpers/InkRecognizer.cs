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

        public IDictionary<uint, InkStroke> strokeMap = new Dictionary<uint, InkStroke>();

        public InkRecognizer(string subscriptionKey, string endpoint, string inkRecognitionUrl)
        {
            httpClient = new HttpClient() { BaseAddress = new Uri(endpoint) };
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            this.inkRecognitionUrl = inkRecognitionUrl;
        }

        public void AddStrokes(IReadOnlyList<InkStroke> strokes)
        {
            foreach (InkStroke stroke in strokes)
            {
                strokeMap[stroke.Id] = stroke;
            }
        }

        public void RemoveStroke(uint strokeId)
        {
            strokeMap.Remove(strokeId);
        }

        public JsonObject ConvertInkToJson()
        {
            const float dipsPerMm = 96 / 25.4f;
            var payload = new JsonObject();
            var strokesArray = new JsonArray();

            foreach (var stroke in strokeMap.Values)
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
            payload["language"] = JsonValue.CreateStringValue("en-US");
            payload["strokes"] = strokesArray;

            return payload;
        }

        public async Task<HttpResponseMessage> RecognizeAsync(JsonObject json)
        {
            try
            {
                string payload = json.Stringify();
                var httpContent = new StringContent(payload, Encoding.UTF8, "application/json");
                var httpResponse = await httpClient.PutAsync(inkRecognitionUrl, httpContent);

                // Throw exception for malformed/unauthorized http requests
                if (httpResponse.StatusCode == HttpStatusCode.BadRequest || httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var errorJson = await httpResponse.Content.ReadAsStringAsync();
                    var error = JsonConvert.DeserializeObject(errorJson);
                    throw new HttpRequestException(error.ToString());
                }
                return httpResponse;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public string FormatJson(string json)
        {
            dynamic parsedJson = JsonConvert.DeserializeObject(json);
            return JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
        }
    }
}
