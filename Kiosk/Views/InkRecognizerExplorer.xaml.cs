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

using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Popups;
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

        NumberFormatInfo culture = CultureInfo.InvariantCulture.NumberFormat;
        Dictionary<int, Tuple<string, Color>> recoText = new Dictionary<int, Tuple<string, Color>>();
        Stack<InkStroke> undoStrokes = new Stack<InkStroke>();
        List<InkStroke> clearedStrokes = new List<InkStroke>();
        bool inkCleared = false;
        const float dipsPerMm = 96 / 25.4f;

        Symbol TouchWriting = (Symbol)0xED5F;

        public InkRecognizerExplorer()
        {
            this.InitializeComponent();

            inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Mouse;

            // Register event handlers for inkCanvas 
            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += InkPresenter_StrokeInputStarted;
            inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;

            inkRecognizer = new ServiceHelpers.InkRecognizer(subscriptionKey, endpoint, inkRecognitionUrl);
        }

        private void InkPresenter_StrokeInputStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            jsonPivot.Visibility = Visibility.Collapsed;
            viewCanvasButton.Visibility = Visibility.Collapsed;
            viewJsonButton.Visibility = Visibility.Visible;
            resultCanvas.Visibility = Visibility.Visible;

            clearedStrokes.Clear();
            inkCleared = false;
        }

        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            foreach (var stroke in args.Strokes)
            {
                undoStrokes.Push(stroke);
            }
        }

        #region Event Handlers
        private void TouchButton_Click(object sender, RoutedEventArgs e)
        {
            if (touchButton.IsChecked == true)
            {
                inkCanvas.InkPresenter.InputDeviceTypes |= CoreInputDeviceTypes.Touch;
            }
            else
            {
                inkCanvas.InkPresenter.InputDeviceTypes &= ~CoreInputDeviceTypes.Touch;
            }
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (inkCleared)
            {
                foreach (var stroke in clearedStrokes)
                {
                    inkCanvas.InkPresenter.StrokeContainer.AddStroke(stroke.Clone());
                }

                clearedStrokes.Clear();
                inkCleared = false;
            }
            else
            {
                var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
                if (strokes.Count > 0)
                {
                    var stroke = strokes[strokes.Count - 1];

                    undoStrokes.Push(stroke);

                    stroke.Selected = true;
                    inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                }
            }
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (undoStrokes.Count > 0)
            {
                var stroke = undoStrokes.Pop();

                inkCanvas.InkPresenter.StrokeContainer.AddStroke(stroke.Clone());
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            foreach (var stroke in strokes)
            {
                clearedStrokes.Add(stroke);
            }

            inkCleared = true;
            inkCanvas.InkPresenter.StrokeContainer.Clear();
            requestJson.Text = string.Empty;
            responseJson.Text = string.Empty;
            resultCanvas.Invalidate();
        }

        private async void RecognizeButton_Click(object sender, RoutedEventArgs e)
        {
            var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            if (strokes.Count > 0)
            {
                if (subscriptionKey == string.Empty)
                {
                    var message = new MessageDialog("No API Key found for the Ink Recognizer Explorer.");
                    await message.ShowAsync();
                    return;
                }

                // Clear result canvas before recognition and rendering of results
                ViewCanvasButton_Click(sender, e);
                requestJson.Text = string.Empty;
                responseJson.Text = string.Empty;
                resultCanvas.Invalidate();

                progressRing.IsActive = true;
                progressRing.Visibility = Visibility.Visible;

                // Convert Ink to JSON for request and display it
                string selectedLanguage = languageDropdown.SelectedItem.ToString();
                inkRecognizer.SetLanguage(selectedLanguage);

                inkRecognizer.strokeMap.Clear();
                foreach (var stroke in strokes)
                {
                    inkRecognizer.AddStroke(stroke);
                }

                JsonObject json = inkRecognizer.ConvertInkToJson();
                requestJson.Text = inkRecognizer.FormatJson(json.Stringify());

                // Recognize Ink from JSON and display response
                inkCanvas.InkPresenter.IsInputEnabled = false;
                var response = await inkRecognizer.RecognizeAsync(json);
                string responseString = await response.Content.ReadAsStringAsync();
                responseJson.Text = inkRecognizer.FormatJson(responseString);

                // Draw result on right side canvas
                resultCanvas.Invalidate();
                inkCanvas.InkPresenter.IsInputEnabled = true;

                progressRing.IsActive = false;
                progressRing.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Clear request/response JSON textboxes if there is no strokes on canvas
                requestJson.Text = string.Empty;
                responseJson.Text = string.Empty;
                resultCanvas.Invalidate();
            }
        }

        private void ViewCanvasButton_Click(object sender, RoutedEventArgs e)
        {
            jsonPivot.Visibility = Visibility.Collapsed;
            viewCanvasButton.Visibility = Visibility.Collapsed;
            viewJsonButton.Visibility = Visibility.Visible;
            resultCanvas.Visibility = Visibility.Visible;
        }

        private void ViewJsonButton_Click(object sender, RoutedEventArgs e)
        {
            resultCanvas.Visibility = Visibility.Collapsed;
            viewJsonButton.Visibility = Visibility.Collapsed;
            viewCanvasButton.Visibility = Visibility.Visible;
            jsonPivot.Visibility = Visibility.Visible;
        }

        // Dispose Win2D resources to avoid memory leak
        void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            this.resultCanvas.RemoveFromVisualTree();
            this.resultCanvas = null;
        }
        #endregion

        #region Draw Results On Canvas
        private void ResultCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            culture = CultureInfo.InvariantCulture.NumberFormat;

            if (!(responseJson.Text == string.Empty))
            {

                var response = JObject.Parse(responseJson.Text);
                var jsonArray = (JArray)response.Property("recognitionUnits").Value;

                foreach (var token in jsonArray)
                {
                    string category = token["category"].ToString();

                    switch (category)
                    {
                        case "inkBullet":
                        case "inkWord":
                            AddText(category, token, sender, args);
                            break;
                        case "line":
                            DrawText(token, sender, args);
                            break;
                        case "inkDrawing":
                            string recognizedObject = token["recognizedObject"].ToString();
                            switch (recognizedObject)
                            {
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
                            break;
                    }
                }

                recoText.Clear();
            }
            else
            {
                args.DrawingSession.Clear(Colors.White);
            }
        }

        private void AddText(string category, JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            string recognizedText = token["recognizedText"].ToString();

            if (recognizedText != null)
            {
                int id = int.Parse(token["id"].ToString());

                float floatX = float.Parse(token["boundingRectangle"]["topX"].ToString(), culture);

                uint strokeId = uint.Parse(token["strokeIds"][0].ToString());
                var color = inkRecognizer.strokeMap[strokeId].DrawingAttributes.Color;

                var text = new Tuple<string, Color>(recognizedText, color);
                recoText.Add(id, text);
            }
        }

        private void DrawText(JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            var childIds = (JArray)token["childIds"];
            var initialTransformation = args.DrawingSession.Transform;

            float floatX = float.Parse(token["boundingRectangle"]["topX"].ToString(), culture);
            float floatY = float.Parse(token["boundingRectangle"]["topY"].ToString(), culture);

            float topLeftX = float.Parse(token["rotatedBoundingRectangle"][0]["x"].ToString(), culture);
            float topLeftY = float.Parse(token["rotatedBoundingRectangle"][0]["y"].ToString(), culture);

            float topRightX = float.Parse(token["rotatedBoundingRectangle"][1]["x"].ToString(), culture);
            float topRightY = float.Parse(token["rotatedBoundingRectangle"][1]["y"].ToString(), culture);

            float bottomRightX = float.Parse(token["rotatedBoundingRectangle"][2]["x"].ToString(), culture);
            float bottomRightY = float.Parse(token["rotatedBoundingRectangle"][2]["y"].ToString(), culture);

            float bottomLeftX = float.Parse(token["rotatedBoundingRectangle"][3]["x"].ToString(), culture);
            float bottomLeftY = float.Parse(token["rotatedBoundingRectangle"][3]["y"].ToString(), culture);

            float width = (topRightX - topLeftX) * dipsPerMm;
            float height = (float)Math.Sqrt((Math.Pow((bottomRightX - topRightX), 2)) + (Math.Pow((bottomRightY - topRightY), 2))) * dipsPerMm;

            if (height < 45)
            {
                height = 45;
            }

            float centerPointX = ((topLeftX + topRightX) / 2) * dipsPerMm;
            float centerPointY = ((topLeftY + bottomLeftY) / 2) * dipsPerMm;
            var centerPoint = new Vector2(centerPointX, centerPointY);

            float slope = (bottomRightY - bottomLeftY) / (bottomRightX - bottomLeftX);
            var radians = Math.Atan(slope);
            args.DrawingSession.Transform = Matrix3x2.CreateRotation((float)radians, centerPoint);

            var textFormat = new CanvasTextFormat()
            {
                FontSize = height,
                WordWrapping = CanvasWordWrapping.NoWrap,
                FontFamily = "Ink Free"
            };

            string text = string.Empty;
            foreach (var item in childIds)
            {
                int id = int.Parse(item.ToString());

                text += recoText[id].Item1 + " ";
            }

            var textLayout = new CanvasTextLayout(sender.Device, text, textFormat, width, height);

            int index = 0;
            foreach (var item in childIds)
            {
                int id = int.Parse(item.ToString());

                textLayout.SetColor(index, recoText[id].Item1.Length, recoText[id].Item2);

                index += recoText[id].Item1.Length + 1;
            }

            args.DrawingSession.DrawTextLayout(textLayout, floatX * dipsPerMm, floatY * dipsPerMm, Colors.Black);
            args.DrawingSession.Transform = initialTransformation;
        }

        private void DrawCircle(JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            float floatX = float.Parse(token["center"]["x"].ToString(), culture);
            float floatY = float.Parse(token["center"]["y"].ToString(), culture);
            var centerPoint = new Vector2(floatX * dipsPerMm, floatY * dipsPerMm);

            float diameter = float.Parse(token["boundingRectangle"]["width"].ToString(), culture);

            uint strokeId = uint.Parse(token["strokeIds"][0].ToString());
            var color = inkRecognizer.strokeMap[strokeId].DrawingAttributes.Color;

            Size size = inkRecognizer.strokeMap[strokeId].DrawingAttributes.Size;
            float strokeWidth = (float)size.Width;

            args.DrawingSession.DrawCircle(centerPoint, (diameter * dipsPerMm) / 2, color, strokeWidth);
        }

        private void DrawEllipse(JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            float floatX = float.Parse(token["center"]["x"].ToString(), culture);
            float floatY = float.Parse(token["center"]["y"].ToString(), culture);
            var centerPoint = new Vector2(floatX * dipsPerMm, floatY * dipsPerMm);

            float diameterX = float.Parse(token["boundingRectangle"]["width"].ToString(), culture);
            float diameterY = float.Parse(token["boundingRectangle"]["height"].ToString(), culture);

            uint strokeId = uint.Parse(token["strokeIds"][0].ToString());
            var color = inkRecognizer.strokeMap[strokeId].DrawingAttributes.Color;

            Size size = inkRecognizer.strokeMap[strokeId].DrawingAttributes.Size;
            float strokeWidth = (float)size.Width;

            args.DrawingSession.DrawEllipse(centerPoint, (diameterX * dipsPerMm) / 2, (diameterY * dipsPerMm) / 2, color, strokeWidth);
        }

        private void DrawLine(JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            float height = float.Parse(token["boundingRectangle"]["height"].ToString(), culture);
            float width = float.Parse(token["boundingRectangle"]["width"].ToString(), culture);
     
            if (height <= 10 && width >= 20)
            {
                float pointAX = float.Parse(token["rotatedBoundingRectangle"][0]["x"].ToString(), culture);
                float pointAY = float.Parse(token["rotatedBoundingRectangle"][0]["y"].ToString(), culture);
                var pointA = new Vector2(pointAX * dipsPerMm, pointAY * dipsPerMm);

                float pointBX = float.Parse(token["rotatedBoundingRectangle"][1]["x"].ToString(), culture);
                var pointB = new Vector2(pointBX * dipsPerMm, pointAY * dipsPerMm);

                uint strokeId = uint.Parse(token["strokeIds"][0].ToString());
                var color = inkRecognizer.strokeMap[strokeId].DrawingAttributes.Color;

                Size size = inkRecognizer.strokeMap[strokeId].DrawingAttributes.Size;
                float strokeWidth = (float)size.Width;

                args.DrawingSession.DrawLine(pointA, pointB, color, strokeWidth);
            }
        }

        private void DrawPolygon(JToken token, CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (token["points"].HasValues)
            {
                float floatX = float.Parse(token["center"]["x"].ToString(), culture);
                float floatY = float.Parse(token["center"]["y"].ToString(), culture);
                var centerPoint = new Vector2(floatX / dipsPerMm, floatY / dipsPerMm);

                var pointList = new List<Vector2>();
                foreach (var item in token["points"])
                {
                    float x = float.Parse(item["x"].ToString(), culture);
                    float y = float.Parse(item["y"].ToString(), culture);

                    var point = new Vector2(x * dipsPerMm, y * dipsPerMm);

                    pointList.Add(point);
                }

                var points = pointList.ToArray();
                var shape = CanvasGeometry.CreatePolygon(sender.Device, points);

                var strokeIds = (JArray)token["strokeIds"];
                uint strokeId = uint.MaxValue;
                foreach (var item in strokeIds)
                {
                    var id = uint.Parse(item.ToString());
                    if (id < strokeId)
                    {
                        strokeId = id;
                    }
                }
                var color = inkRecognizer.strokeMap[strokeId].DrawingAttributes.Color;

                Size size = inkRecognizer.strokeMap[strokeId].DrawingAttributes.Size;
                float strokeWidth = (float)size.Width;

                args.DrawingSession.DrawGeometry(shape, centerPoint, color, strokeWidth);
            }

        }
        #endregion
    }
}
