// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Cognitive Services: 
// http://www.microsoft.com/cognitive
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

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServiceHelpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Numerics;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace IntelligentKioskSample.Views
{
    [KioskExperience(Title = "Ink Recognizer Explorer", ImagePath = "ms-appx:/Assets/InkRecognizerExplorer.png")]
    public sealed partial class InkRecognizerExplorer : Page
    {
        // API key and endpoint information for ink recognition request
        string subscriptionKey = SettingsHelper.Instance.InkRecognizerApiKey;
        const string endpoint = "https://api.cognitive.microsoft.com";
        const string inkRecognitionUrl = "/inkrecognizer/v1.0-preview/recognize";

        ServiceHelpers.InkRecognizer inkRecognizer;
        CanvasTextFormat textFormat;
        NumberFormatInfo culture;

        // Timer to be used to trigger ink recognition
        private readonly DispatcherTimer dispatcherTimer;
        const double IDLE_TIME = 500;

        const float dipsPerMm = 96 / 25.4f;
        const float strokeWidth = 5;

        public InkRecognizerExplorer()
        {
            this.InitializeComponent();

            inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Mouse;
            
            // Register event handlers for inkCanvas 
            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += InkPresenter_StrokeInputStarted;
            inkCanvas.InkPresenter.StrokeInput.StrokeEnded += InkPresenter_StrokeInputEnded;
            inkCanvas.InkPresenter.StrokesCollected += InkPresenter_StrokesCollected;
            inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;

            inkRecognizer = new ServiceHelpers.InkRecognizer(subscriptionKey, endpoint, inkRecognitionUrl);

            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(IDLE_TIME);
        }

        private void InkPresenter_StrokeInputStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            StopTimer();
        }

        private void InkPresenter_StrokeInputEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            StartTimer();
        }

        private void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            StopTimer();

            inkRecognizer.AddStrokes(args.Strokes);

            StartTimer();
        }

        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            StopTimer();

            foreach (var stroke in args.Strokes)
            {
                inkRecognizer.RemoveStroke(stroke.Id);
            }

            StartTimer();
        }

        public void StartTimer()
        {
            dispatcherTimer.Start();
        }

        public void StopTimer()
        {
            dispatcherTimer.Stop();
        }

        private async void DispatcherTimer_Tick(object sender, object e)
        {
            StopTimer();

            try
            {
                if(inkRecognizer.strokeMap.Count > 0)
                {
                    // Clear result canvas before recognition and rendering of results
                    responseJson.Text = string.Empty;
                    resultCanvas.Invalidate();

                    progressRing.IsActive = true;
                    progressRing.Visibility = Visibility.Visible;
                    progressRingText.Visibility = Visibility.Visible;

                    // Convert Ink to JSON for request and display it
                    JsonObject json = inkRecognizer.ConvertInkToJson();
                    requestJson.Text = inkRecognizer.FormatJson(json.Stringify());

                    // Recognize Ink from JSON and display response
                    var response = await inkRecognizer.RecognizeAsync(json);
                    string responseString = await response.Content.ReadAsStringAsync();
                    responseJson.Text = inkRecognizer.FormatJson(responseString);

                    // Draw result on right side canvas
                    resultCanvas.Invalidate();

                    progressRing.IsActive = false;
                    progressRing.Visibility = Visibility.Collapsed;
                    progressRingText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Clear request/response JSON textboxes if there is no strokes on canvas
                    requestJson.Text = string.Empty;
                    responseJson.Text = string.Empty;
                    resultCanvas.Invalidate();
                }
            }

            catch (Exception ex)
            {
                responseJson.Text = ex.Message;
            }
        }

        // Dispose Win2D resources to avoid memory leak
        void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            this.resultCanvas.RemoveFromVisualTree();
            this.resultCanvas = null;
        }

        private void ResultCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            textFormat = new CanvasTextFormat();
            culture = CultureInfo.InvariantCulture.NumberFormat;

            if (!(responseJson.Text == string.Empty))
            {
                var response = JObject.Parse(responseJson.Text);
                var jsonArray = (JArray)response.Property("recognitionUnits").Value;

                foreach (var token in jsonArray)
                {
                    string category = token.Value<string>("category");
                    
                    if (category == "line")
                    {
                        string recognizedText = token.Value<string>("recognizedText");
                        if (recognizedText != null)
                        {
                            DrawText(recognizedText, token, sender, args);
                        }
                    }
                    if (category == "inkDrawing")
                    {
                        string recognizedObject = token.Value<string>("recognizedObject");

                        switch (recognizedObject)
                        {
                            case "square":
                            case "rectangle":
                                DrawRectangle(token, sender, args);
                                break;
                            case "circle":
                                DrawCircle(token, sender, args);
                                break;
                            case "ellipse":
                                DrawEllipse(token, sender, args);
                                break;
                            case "drawing":
                                DrawLine(token, sender, args);
                                break;
                            default:
                                DrawPolygon(token, sender, args);
                                break;
                        }
                    }
                }
            }
            else
            {
                args.DrawingSession.Clear(Colors.White);
            }
        }

        private void DrawText(string recognizedText, JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            float floatX = float.Parse(token["boundingRectangle"]["topX"].ToString(), culture);
            float floatY = float.Parse(token["boundingRectangle"]["topY"].ToString(), culture);

            float fontSize = float.Parse(token["boundingRectangle"]["height"].ToString(), culture);
            textFormat.FontSize = fontSize * dipsPerMm;
            
            args.DrawingSession.DrawText(recognizedText, floatX * dipsPerMm, floatY * dipsPerMm, Colors.Black, textFormat);
        }

        private void DrawRectangle(JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            float floatX = float.Parse(token["boundingRectangle"]["topX"].ToString(), culture);
            float floatY = float.Parse(token["boundingRectangle"]["topY"].ToString(), culture);
            float height = float.Parse(token["boundingRectangle"]["height"].ToString(), culture);
            float width = float.Parse(token["boundingRectangle"]["width"].ToString(), culture);

            var rect = new Rect()
            {
                X = floatX * dipsPerMm,
                Y = floatY * dipsPerMm,
                Height = height * dipsPerMm,
                Width = width * dipsPerMm
            };

            args.DrawingSession.DrawRectangle(rect, Colors.Black, strokeWidth);
        }

        private void DrawCircle(JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            float floatX = float.Parse(token["center"]["x"].ToString(), culture);
            float floatY = float.Parse(token["center"]["y"].ToString(), culture);
            var centerPoint = new Vector2(floatX * dipsPerMm, floatY * dipsPerMm);

            float diameter = float.Parse(token["boundingRectangle"]["width"].ToString(), culture);

            args.DrawingSession.DrawCircle(centerPoint, (diameter * dipsPerMm) / 2, Colors.Black, strokeWidth);
        }

        private void DrawEllipse(JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            float floatX = float.Parse(token["center"]["x"].ToString(), culture);
            float floatY = float.Parse(token["center"]["y"].ToString(), culture);
            var centerPoint = new Vector2(floatX * dipsPerMm, floatY * dipsPerMm);

            float diameterX = float.Parse(token["boundingRectangle"]["width"].ToString(), culture);
            float diameterY = float.Parse(token["boundingRectangle"]["height"].ToString(), culture);

            args.DrawingSession.DrawEllipse(centerPoint, (diameterX * dipsPerMm) / 2, (diameterY * dipsPerMm) / 2, Colors.Black, strokeWidth);
        }

        private void DrawLine(JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            float height = float.Parse(token["boundingRectangle"]["height"].ToString(), culture);
            float width = float.Parse(token["boundingRectangle"]["width"].ToString(), culture);
     
            if (height <= 10 && width >= 5)
            {
                float pointAX = float.Parse(token["rotatedBoundingRectangle"][0]["x"].ToString(), culture);
                float pointAY = float.Parse(token["rotatedBoundingRectangle"][0]["y"].ToString(), culture);
                var pointA = new Vector2(pointAX * dipsPerMm, pointAY * dipsPerMm);

                float pointBX = float.Parse(token["rotatedBoundingRectangle"][1]["x"].ToString(), culture);
                var pointB = new Vector2(pointBX * dipsPerMm, pointAY * dipsPerMm);

                args.DrawingSession.DrawLine(pointA, pointB, Colors.Black, strokeWidth);
            }
        }

        private void DrawPolygon(JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (token["points"].HasValues)
            {
                float floatX = float.Parse(token["boundingRectangle"]["topX"].ToString(), culture);
                float floatY = float.Parse(token["boundingRectangle"]["topY"].ToString(), culture);
                var centerPoint = new Vector2(floatX, floatY);

                var pointList = new List<Vector2>();
                foreach (var item in token["points"])
                {
                    float x = float.Parse(item["x"].ToString(), culture);
                    float y = float.Parse(item["y"].ToString(), culture);

                    var point = new Vector2(x * dipsPerMm, y * dipsPerMm);

                    pointList.Add(point);
                }

                var points = pointList.ToArray();
                var shape = CanvasGeometry.CreatePolygon(args.DrawingSession, points);

                args.DrawingSession.DrawGeometry(shape, centerPoint, Colors.Black, strokeWidth);
            }

        }
    }
}
