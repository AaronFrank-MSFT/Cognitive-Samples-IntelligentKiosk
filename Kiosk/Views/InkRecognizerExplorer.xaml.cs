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

using IntelligentKioskSample.Models.InkRecognizerExplorer;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace IntelligentKioskSample.Views.InkRecognizerExplorer
{
    [KioskExperience(Title = "Ink Recognizer Explorer", ImagePath = "ms-appx:/Assets/InkRecognizerExplorer.png")]
    public sealed partial class InkRecognizerExplorer : Page
    {
        // API key and endpoint information for ink recognition request
        string subscriptionKey = SettingsHelper.Instance.InkRecognizerApiKey;
        const string endpoint = "https://api.cognitive.microsoft.com/";
        const string inkRecognitionUrl = "/inkrecognizer/v1.0-preview/recognize";

        ServiceHelpers.InkRecognizer inkRecognizer;
        InkResponse inkResponse;

        Dictionary<int, InkRecognitionUnit> recoTreeNodes;
        List<InkRecognitionUnit> recoTreeParentNodes;
        Dictionary<int, Tuple<string, Color>> recoText;

        Stack<InkStroke> redoStrokes;
        List<InkStroke> clearedStrokes;
        InkToolbarToolButton activeTool;
        bool inkCleared = false;

        const float dipsPerMm = 96 / 25.4f;

        Symbol Undo = (Symbol)0xE7A7;
        Symbol Redo = (Symbol)0xE7A6;
        Symbol ClearAll = (Symbol)0xE74D;
        Symbol TouchWriting = (Symbol)0xED5F;

        public InkRecognizerExplorer()
        {
            this.InitializeComponent();

            inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Mouse;

            // Register event handlers
            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += InkPresenter_StrokeInputStarted;
            inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;

            inkRecognizer = new ServiceHelpers.InkRecognizer(subscriptionKey, endpoint, inkRecognitionUrl);

            recoTreeNodes = new Dictionary<int, InkRecognitionUnit>();
            recoTreeParentNodes = new List<InkRecognitionUnit>();
            recoText = new Dictionary<int, Tuple<string, Color>>();

            redoStrokes = new Stack<InkStroke>();
            clearedStrokes = new List<InkStroke>();
            activeTool = ballpointPen;

            customToolbar.ActiveTool = null;
        }

        #region Event Handlers
        protected override async void OnNavigatedTo(NavigationEventArgs e)
            {
                if (string.IsNullOrEmpty(SettingsHelper.Instance.InkRecognizerApiKey))
                {
                    await new MessageDialog("Missing Ink Recognizer API Key. Please enter a key in the Settings page.", "Missing API Key").ShowAsync();
                }
                else
                {
                    var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/InkRecognitionSampleInstructions.gif"));
                    if (file != null)
                    {
                        using (var stream = await file.OpenSequentialReadAsync())
                        {
                            await inkCanvas.InkPresenter.StrokeContainer.LoadAsync(stream);
                        }
                    }

                    RecognizeButton_Click(null, null);
                }

                base.OnNavigatedTo(e);
            }

        private void InkPresenter_StrokeInputStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            ViewCanvasButton_Click(null, null);

            clearedStrokes.Clear();
            inkCleared = false;

            activeTool = inkToolbar.ActiveTool;
        }

        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            // When strokes are erased they are treated the same as an undo and are pushed onto a stack of strokes for the redo button
            foreach (var stroke in args.Strokes)
            {
                redoStrokes.Push(stroke);
            }

            activeTool = inkToolbar.ActiveTool;
        }

        private void CustomToolbar_ActiveToolChanged(InkToolbar sender, object args)
        {            
            customToolbar.ActiveTool.IsChecked = false;
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            undoButton.IsChecked = false;

            if (inkCleared)
            {
                foreach (var stroke in clearedStrokes)
                {
                    inkCanvas.InkPresenter.StrokeContainer.AddStroke(stroke.Clone());
                }

                clearedStrokes.Clear();
                inkCleared = false;
            }
            else if (activeTool is InkToolbarEraserButton)
            {
                RedoButton_Click(null, null);
            }
            else
            {
                var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
                if (strokes.Count > 0)
                {
                    var stroke = strokes[strokes.Count - 1];

                    redoStrokes.Push(stroke);

                    stroke.Selected = true;
                    inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                }
            }
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            redoButton.IsChecked = false;

            if (redoStrokes.Count > 0)
            {
                var stroke = redoStrokes.Pop();

                inkCanvas.InkPresenter.StrokeContainer.AddStroke(stroke.Clone());
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            clearButton.IsChecked = false;

            var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            foreach (var stroke in strokes)
            {
                clearedStrokes.Add(stroke);
            }

            inkCleared = true;
            inkCanvas.InkPresenter.StrokeContainer.Clear();
            ClearJson();
        }

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

        private async void RecognizeButton_Click(object sender, RoutedEventArgs e)
        {
            var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            if (strokes.Count > 0)
            {
                // Clear result canvas and viewable JSON before recognition and rendering of results
                ViewCanvasButton_Click(null, null);
                ClearJson();

                // Convert Ink to JSON for request and display it
                string selectedLanguage = languageDropdown.SelectedItem.ToString();
                inkRecognizer.SetLanguage(selectedLanguage);

                inkRecognizer.StrokeMap.Clear();
                foreach (var stroke in strokes)
                {
                    inkRecognizer.AddStroke(stroke);
                }

                JsonObject json = inkRecognizer.ConvertInkToJson();
                requestJson.Text = FormatJson(json.Stringify());

                // Disable use of toolbar during recognition and rendering
                ToggleInkToolbar();
                ToggleProgressRing();

                // Recognize Ink from JSON and display response
                var response = await inkRecognizer.RecognizeAsync(json);
                string responseString = await response.Content.ReadAsStringAsync();
                inkResponse = JsonConvert.DeserializeObject<InkResponse>(responseString);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // Generate JSON tree view and draw result on right side canvas
                    CreateJsonTree();
                    responseJson.Text = FormatJson(responseString);
                    resultCanvas.Invalidate();
                }
                else
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        await new MessageDialog("Please validate the Ink Recognizer API key used in the settings page is correct", "Unauthorized Request").ShowAsync();
                    }
                    else
                    {
                        await new MessageDialog(inkResponse.Error.message, $"Response Code: {inkResponse.Error.code}").ShowAsync();
                    }
                }

                // Re-enable use of toolbar after recognition and rendering
                ToggleInkToolbar();
                ToggleProgressRing();
            }
            else
            {
                // Clear viewable JSON if there is no strokes on canvas
                ClearJson();
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

        private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            var node = (TreeViewNode)args.InvokedItem;

            if (node.IsExpanded == false)
            {
                node.IsExpanded = true;
            }
            else
            {
                node.IsExpanded = false;
            }
        }

        private void ResultCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (!string.IsNullOrEmpty(responseJson.Text))
            {
                foreach (var recoUnit in inkResponse.RecognitionUnits)
                {
                    string category = recoUnit.category;
                    switch (category)
                    {
                        case "inkBullet":
                        case "inkWord":
                            AddText(recoUnit, sender, args);
                            break;
                        case "line":
                            DrawText(recoUnit, sender, args);
                            break;
                        case "inkDrawing":
                            string recognizedObject = recoUnit.recognizedObject;
                            switch (recognizedObject)
                            {
                                case "circle":
                                    DrawCircle(recoUnit, sender, args);
                                    break;
                                case "ellipse":
                                    DrawEllipse(recoUnit, sender, args);
                                    break;
                                case "drawing":
                                    DrawLine(recoUnit, sender, args);
                                    break;
                                default:
                                    DrawPolygon(recoUnit, sender, args);
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

        void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            // Dispose Win2D resources to avoid memory leak
            this.resultCanvas.RemoveFromVisualTree();
            this.resultCanvas = null;
        }
        #endregion

        #region Draw Results On Canvas
        private void AddText(InkRecognitionUnit recoUnit, CanvasControl sender, CanvasDrawEventArgs args)
        {
            string recognizedText = recoUnit.recognizedText;

            if (recognizedText != null)
            {
                int id = recoUnit.id;

                // Color of ink word or ink bullet
                var color = GetStrokeColor(recoUnit);

                var text = new Tuple<string, Color>(recognizedText, color);
                recoText.Add(id, text);
            }
        }

        private void DrawText(InkRecognitionUnit recoUnit, CanvasControl sender, CanvasDrawEventArgs args)
        {
            var childIds = recoUnit.childIds;
            var initialTransformation = args.DrawingSession.Transform;

            // Points of bounding rectangle to align drawn text
            float floatX = (float)recoUnit.boundingRectangle.topX;
            float floatY = (float)recoUnit.boundingRectangle.topY;

            // Rotated bounding rectangle points to get correct angle of text being drawn
            float topLeftX = (float)recoUnit.rotatedBoundingRectangle[0].x;
            float topLeftY = (float)recoUnit.rotatedBoundingRectangle[0].y;

            float topRightX = (float)recoUnit.rotatedBoundingRectangle[1].x;
            float topRightY = (float)recoUnit.rotatedBoundingRectangle[1].y;

            float bottomRightX = (float)recoUnit.rotatedBoundingRectangle[2].x;
            float bottomRightY = (float)recoUnit.rotatedBoundingRectangle[2].y;

            float bottomLeftX = (float)recoUnit.rotatedBoundingRectangle[3].x;
            float bottomLeftY = (float)recoUnit.rotatedBoundingRectangle[3].y;

            // Height and width of bounding rectangle to get font size and width for text layout
            float height = GetDistanceBetweenPoints(topRightX, bottomRightX, topRightY, bottomRightY) * dipsPerMm;
            float width = GetDistanceBetweenPoints(bottomLeftX, bottomRightX, bottomLeftY, bottomRightY) * dipsPerMm;

            if (height < 45)
            {
                height = 45;
            }

            // Transform to get correct angle of text
            float centerPointX = ((topLeftX + topRightX) / 2) * dipsPerMm;
            float centerPointY = ((topLeftY + bottomLeftY) / 2) * dipsPerMm;
            var centerPoint = new Vector2(centerPointX, centerPointY);

            Matrix3x2 angle = GetRotationAngle(bottomLeftX, bottomRightX, bottomLeftY, bottomRightY, centerPoint);

            args.DrawingSession.Transform = angle;

            var textFormat = new CanvasTextFormat()
            {
                FontSize = height - 5,
                WordWrapping = CanvasWordWrapping.NoWrap,
                FontFamily = "Ink Free"
            };

            // Build string to be drawn to canvas
            string text = string.Empty;
            foreach (var item in childIds)
            {
                int id = int.Parse(item.ToString());

                text += recoText[id].Item1 + " ";
            }

            var textLayout = new CanvasTextLayout(sender.Device, text, textFormat, width, height);

            // Associate correct color with each word in string
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

        private void DrawCircle(InkRecognitionUnit recoUnit, CanvasControl sender, CanvasDrawEventArgs args)
        {
            // Center point and diameter of circle
            float floatX = (float)recoUnit.center.x;
            float floatY = (float)recoUnit.center.y;
            var centerPoint = new Vector2(floatX * dipsPerMm, floatY * dipsPerMm);

            float diameter = (float)recoUnit.boundingRectangle.width;

            // Color of circle
            var color = GetStrokeColor(recoUnit);

            // Stroke thickness
            var strokeWidth = GetStrokeWidth(recoUnit);

            args.DrawingSession.DrawCircle(centerPoint, (diameter * dipsPerMm) / 2, color, strokeWidth);
        }

        private void DrawEllipse(InkRecognitionUnit recoUnit, CanvasControl sender, CanvasDrawEventArgs args)
        {
            var initialTransformation = args.DrawingSession.Transform;

            // Rotated bounding rectangle points to get correct angle of ellipse being drawn
            float floatX = (float)recoUnit.boundingRectangle.topX;
            float floatY = (float)recoUnit.boundingRectangle.topY;

            float topLeftX = (float)recoUnit.rotatedBoundingRectangle[0].x;
            float topLeftY = (float)recoUnit.rotatedBoundingRectangle[0].y;

            float topRightX = (float)recoUnit.rotatedBoundingRectangle[1].x;
            float topRightY = (float)recoUnit.rotatedBoundingRectangle[1].y;

            float bottomRightX = (float)recoUnit.rotatedBoundingRectangle[2].x;
            float bottomRightY = (float)recoUnit.rotatedBoundingRectangle[2].y;

            float bottomLeftX = (float)recoUnit.rotatedBoundingRectangle[3].x;
            float bottomLeftY = (float)recoUnit.rotatedBoundingRectangle[3].y;

            // Center point of ellipse
            float centerPointX = (float)recoUnit.center.x;
            float centerPointY = (float)recoUnit.center.y;

            var centerPoint = new Vector2(centerPointX * dipsPerMm, centerPointY * dipsPerMm);

            // X and Y diameter of ellipse
            float diameterX = GetDistanceBetweenPoints(bottomLeftX, bottomRightX, bottomLeftY, bottomRightY) * dipsPerMm;            
            float diameterY = GetDistanceBetweenPoints(topRightX, bottomRightX, topRightY, bottomRightY) * dipsPerMm;

            // Transform to get correct angle of ellipse
            float transformCenterPointX = ((topLeftX + topRightX) / 2) * dipsPerMm;
            float transformCenterPointY = ((topLeftY + bottomLeftY) / 2) * dipsPerMm;
            var transformCenterPoint = new Vector2(transformCenterPointX, transformCenterPointY);

            Matrix3x2 angle = GetRotationAngle(bottomLeftX, bottomRightX, bottomLeftY, bottomRightY, transformCenterPoint);

            args.DrawingSession.Transform = angle;

            // Color of ellipse
            var color = GetStrokeColor(recoUnit);

            // Stroke thickness
            float strokeWidth = GetStrokeWidth(recoUnit);

            args.DrawingSession.DrawEllipse(centerPoint, diameterX / 2, diameterY / 2, color, strokeWidth);
            args.DrawingSession.Transform = initialTransformation;
        }

        private void DrawLine(InkRecognitionUnit recoUnit, CanvasControl sender, CanvasDrawEventArgs args)
        {
            float height = (float)recoUnit.boundingRectangle.height;
            float width = (float)recoUnit.boundingRectangle.width;

            if (height <= 10 && width >= 20)
            {
                // Bottom left and right corner points of rotated bounding rectangle
                float pointAX = (float)recoUnit.rotatedBoundingRectangle[0].x;
                float pointAY = (float)recoUnit.rotatedBoundingRectangle[0].y;
                float pointBX = (float)recoUnit.rotatedBoundingRectangle[1].x;

                var pointA = new Vector2(pointAX * dipsPerMm, pointAY * dipsPerMm);
                var pointB = new Vector2(pointBX * dipsPerMm, pointAY * dipsPerMm);

                // Color of line
                var color = GetStrokeColor(recoUnit);

                // Stroke thickness
                float strokeWidth = GetStrokeWidth(recoUnit);

                args.DrawingSession.DrawLine(pointA, pointB, color, strokeWidth);
            }
        }

        private void DrawPolygon(InkRecognitionUnit recoUnit, CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (recoUnit.points.Count > 0)
            {
                // Center point of polygon
                float floatX = (float)recoUnit.center.x;
                float floatY = (float)recoUnit.center.y;
                var centerPoint = new Vector2(floatX / dipsPerMm, floatY / dipsPerMm);

                // Create new list of points for polygon to be drawn
                var pointList = new List<Vector2>();
                foreach (var inkPoint in recoUnit.points)
                {
                    float x = (float)inkPoint.x;
                    float y = (float)inkPoint.y;

                    var point = new Vector2(x * dipsPerMm, y * dipsPerMm);

                    pointList.Add(point);
                }

                var points = pointList.ToArray();
                var shape = CanvasGeometry.CreatePolygon(sender.Device, points);

                // Color of polygon
                var color = GetStrokeColor(recoUnit);

                // Stroke thickness
                float strokeWidth = GetStrokeWidth(recoUnit);

                args.DrawingSession.DrawGeometry(shape, centerPoint, color, strokeWidth);
            }

        }
        #endregion

        #region JSON To Tree View
        private void CreateJsonTree()
        {
            // Add all of the ink recognition units that will become nodes to a collection
            foreach (var recoUnit in inkResponse.RecognitionUnits)
            {
                recoTreeNodes.Add(recoUnit.id, recoUnit);
                var count = recoTreeNodes.Count;

                // If the ink recognition unit is a top level node in the tree add it to a linked list to preserve order
                if (recoUnit.parentId == 0)
                {
                    recoTreeParentNodes.Add(recoUnit);
                }
            }

            // Add the initial "root" node for all of the ink recognition units to be added under
            string itemCount = $"{recoTreeParentNodes.Count} item{(recoTreeParentNodes.Count > 1 ? "s" : string.Empty)}";
            var root = new TreeViewNode()
            {
                Content = new KeyValuePair<string, string>("Root", itemCount)
            };

            // Traverse the linked list of top level parent nodes and append children if they have any
            foreach (var parent in recoTreeParentNodes)
            {
                string category = parent.category;

                var node = new TreeViewNode();

                if (category == "writingRegion")
                {
                    string childCount = $"{parent.childIds.Count} item{(parent.childIds.Count > 1 ? "s" : string.Empty)}";

                    node.Content = new KeyValuePair<string, string>(category, childCount);

                    // Recursively append all children
                    var childNodes = GetChildNodes(parent);
                    foreach (var child in childNodes)
                    {
                        node.Children.Add(child);
                    }
                }
                else
                {
                    node.Content = new KeyValuePair<string, string>(category, parent.recognizedObject);
                }

                root.Children.Add(node);
            }

            root.IsExpanded = true;
            treeView.RootNodes.Add(root);
        }

        private List<TreeViewNode> GetChildNodes(InkRecognitionUnit recoUnit)
        {
            var nodes = new List<TreeViewNode>();

            // Iterate over each of the ink recognition unit's children to append them to their parent node
            foreach (int id in recoUnit.childIds)
            {
                InkRecognitionUnit unit = recoTreeNodes[id];
                var node = new TreeViewNode();

                string category = unit.category;
                if (category == "inkWord" || category == "inkBullet")
                {
                    node.Content = new KeyValuePair<string, string>(category, unit.recognizedText);
                }
                else
                {
                    string childCount = $"{unit.childIds.Count} item{(unit.childIds.Count > 1 ? "s" : string.Empty)}";
                    node.Content = new KeyValuePair<string, string>(category, childCount);
                }

                // If the child of the current ink recognition unit also has children recurse to append them to the child node as well
                if (unit.childIds != null)
                {
                    var childNodes = GetChildNodes(unit);
                    foreach (var child in childNodes)
                    {
                        node.Children.Add(child);
                    }
                }

                nodes.Add(node);
            }

            return nodes;
        }
        #endregion

        #region Helpers
        private void ClearJson()
        {
            requestJson.Text = string.Empty;
            responseJson.Text = string.Empty;
            treeView.RootNodes.Clear();
            recoTreeNodes.Clear();
            recoTreeParentNodes.Clear();
            resultCanvas.Invalidate();
        }

        private string FormatJson(string json)
        {
            dynamic parsedJson = JsonConvert.DeserializeObject(json);
            return JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
        }

        private void ToggleProgressRing()
        {
            if (progressRing.IsActive == false)
            {
                recognizeButtonText.Opacity = 0;
                progressRing.IsActive = true;
                progressRing.Visibility = Visibility.Visible;
            }
            else
            {
                recognizeButtonText.Opacity = 100;
                progressRing.IsActive = false;
                progressRing.Visibility = Visibility.Collapsed;
            }
        }

        private void ToggleInkToolbar()
        {
            if (inkToolbar.IsEnabled && customToolbar.IsEnabled)
            {
                inkToolbar.IsEnabled = false;
                customToolbar.IsEnabled = false;
                inkCanvas.InkPresenter.IsInputEnabled = false;
            }
            else
            {
                inkToolbar.IsEnabled = true;
                customToolbar.IsEnabled = true;
                inkCanvas.InkPresenter.IsInputEnabled = true;
            }
        }

        private float GetDistanceBetweenPoints(float x1, float x2, float y1, float y2)
        {
            float x = (x2 - x1) * (x2 - x1);
            float y = (y2 - y1) * (y2 - y1);

            float distance = (float)Math.Sqrt(x + y);

            return distance;
        }

        private Matrix3x2 GetRotationAngle(float x1, float x2, float y1, float y2, Vector2 centerPoint)
        {
            float slope = (y2 - y1) / (x2 - x1);
            float radians = (float)Math.Atan(slope);

            var angle = Matrix3x2.CreateRotation(radians, centerPoint);

            return angle;
        }

        private Color GetStrokeColor(InkRecognitionUnit recoUnit)
        {
            uint strokeId = (uint)recoUnit.strokeIds[0];
            var color = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(strokeId).DrawingAttributes.Color;

            return color;
        }

        private float GetStrokeWidth(InkRecognitionUnit recoUnit)
        {
            uint strokeId = (uint)recoUnit.strokeIds[0];

            Size size = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(strokeId).DrawingAttributes.Size;
            float strokeWidth = (float)size.Width;

            return strokeWidth;
        }
        #endregion
    }
}
