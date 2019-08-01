// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Cognitive Services: http://www.microsoft.com/cognitive
// 
// Microsoft Cognitive Services Github:
// https://github.com/Microsoft/Cognitive
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Graphics.Display;
using Windows.UI.Input.Inking;

namespace ServiceHelpers
{
    public class InkRecognizer
    {
        string inkRecognitionUrl;
        HttpClient httpClient;

        private IDictionary<uint, InkStroke> StrokeMap { get; set; }
        private string LanguageCode { get; set; }

        public InkRecognizer(string subscriptionKey, string endpoint, string inkRecognitionUrl)
        {
            httpClient = new HttpClient() { BaseAddress = new Uri(endpoint) };
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            this.inkRecognitionUrl = inkRecognitionUrl;

            StrokeMap = new Dictionary<uint, InkStroke>();
            LanguageCode = "en-US";
        }

        public void AddStrokes(IReadOnlyList<InkStroke> strokes)
        {
            foreach (var stroke in strokes)
            {
                StrokeMap[stroke.Id] = stroke;
            }
        }

        public void ClearStrokes()
        {
            StrokeMap.Clear();
        }

        public void SetLanguage(string languageCode)
        {
            LanguageCode = languageCode;
        }

        public JsonObject ConvertInkToJson()
        {
            // If needed to use the device's DPI, and example is below. Whatever DPI is used below will need to be used again when handling the response's ink points.
            //var displayInformation = DisplayInformation.GetForCurrentView();
            //float dpi = displayInformation.LogicalDpi;
            //float dipsPerMm = dpi / 25.4f;

            float dipsPerMm = 96 / 25.4f;

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
