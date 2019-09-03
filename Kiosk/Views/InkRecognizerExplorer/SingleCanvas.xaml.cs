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
using Point = Windows.Foundation.Point;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Numerics;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace IntelligentKioskSample.Views.InkRecognizerExplorer
{
    public sealed partial class SingleCanvas : Page
    {
        // API key and endpoint information for ink recognition request
        private string subscriptionKey = SettingsHelper.Instance.InkRecognizerApiKey;
        private string endpoint = SettingsHelper.Instance.InkRecognizerApiKeyEndpoint;
        private const string inkRecognitionUrl = "/inkrecognizer/v1.0-preview/recognize";

        private ServiceHelpers.InkRecognizer inkRecognizer;
        private InkResponse inkResponse;

        private Dictionary<string, List<InkStroke>> hiddenStrokes;
        private Dictionary<string, Tuple<CanvasTextLayout, Point>> wordTextLayouts;
        private Dictionary<string, Tuple<CanvasTextLayout, Point>> lineTextLayouts;
        private Dictionary<string, Tuple<CanvasTextLayout, Point>> drawnTextLayouts;

        private Dictionary<int, InkRecognitionUnit> recoTreeNodes;
        private List<InkRecognitionUnit> recoTreeParentNodes;
        private Dictionary<int, Tuple<string, Color>> recoText;

        private Stack<InkStroke> redoStrokes;
        private List<InkStroke> clearedStrokes;
        private InkToolbarToolButton activeTool;
        private bool inkCleared = false;

        private float dipsPerMm;

        private Symbol TouchWriting = (Symbol)0xED5F;
        private Symbol Undo = (Symbol)0xE7A7;
        private Symbol Redo = (Symbol)0xE7A6;
        private Symbol ClearAll = (Symbol)0xE74D;

        public SingleCanvas()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Enabled;

            hiddenStrokes = new Dictionary<string, List<InkStroke>>();
            wordTextLayouts = new Dictionary<string, Tuple<CanvasTextLayout, Point>>();
            lineTextLayouts = new Dictionary<string, Tuple<CanvasTextLayout, Point>>();
            drawnTextLayouts = new Dictionary<string, Tuple<CanvasTextLayout, Point>>();

            recoTreeNodes = new Dictionary<int, InkRecognitionUnit>();
            recoTreeParentNodes = new List<InkRecognitionUnit>();
            recoText = new Dictionary<int, Tuple<string, Color>>();

            redoStrokes = new Stack<InkStroke>();
            clearedStrokes = new List<InkStroke>();
            activeTool = ballpointPen;

            inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Mouse;
            inkCanvas.InkPresenter.StrokeInput.StrokeStarted += InkPresenter_StrokeInputStarted;
            inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;

            inkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;
            inkCanvas.InkPresenter.UnprocessedInput.PointerPressed += InkCanvas_Tapped;

            var languages = new List<Language>
            {
                new Language("Chinese (Simplified)", "zh-CN"),
                new Language("Chinese (Traditional)", "zh-TW"),
                new Language("English (UK)", "en-GB"),
                new Language("English (US)", "en-US"),
                new Language("French", "fr-FR"),
                new Language("German", "de-DE"),
                new Language("Italian", "it-IT"),
                new Language("Korean", "ko-KR"),
                new Language("Portuguese", "pt-PT"),
                new Language("Spanish", "es-ES")
            };

            languageDropdown.ItemsSource = languages;
        }

        #region Event Handlers - Page
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (string.IsNullOrEmpty(SettingsHelper.Instance.InkRecognizerApiKey))
            {
                await new MessageDialog("Missing Ink Recognizer API Key. Please enter a key in the Settings page.", "Missing API Key").ShowAsync();
            }

            // When the page is Unloaded, InkRecognizer and the Win2D CanvasControl are disposed. To preserve the state of the page we need to re-instantiate these objects.
            // In the case of the Win2D CanvasControl, a new UI Element needs to be created/appended to the page as well
            inkRecognizer = new ServiceHelpers.InkRecognizer(subscriptionKey, endpoint, inkRecognitionUrl);
            dipsPerMm = inkRecognizer.GetDipsPerMm(96);

            resultCanvas = new CanvasControl();
            resultCanvas.Name = "resultCanvas";
            resultCanvas.SetValue(Grid.ColumnSpanProperty, 4);
            resultCanvas.SetValue(Grid.RowProperty, 1);

            canvasGrid.Children.Prepend(resultCanvas);

            RecognizeButton_Click(null, null);
            base.OnNavigatedTo(e);
        }

        void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // Calling Dispose() on InkRecognizer to dispose of resources being used by HttpClient
            inkRecognizer.Dispose();

            // Dispose Win2D resources to avoid memory leak
            // Reference: https://microsoft.github.io/Win2D/html/RefCycles.htm
            var resultCanvas = this.FindName("resultCanvas") as CanvasControl;
            resultCanvas.RemoveFromVisualTree();
            resultCanvas = null;
        }
        #endregion

        #region Event Handlers - Ink Toolbar
        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as InkToolbarCustomToolButton;
            button.IsChecked = false;

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
            var button = sender as InkToolbarCustomToolButton;
            button.IsChecked = false;

            if (redoStrokes.Count > 0)
            {
                var stroke = redoStrokes.Pop();

                inkCanvas.InkPresenter.StrokeContainer.AddStroke(stroke.Clone());
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as InkToolbarCustomToolButton;
            button.IsChecked = false;

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
        #endregion

        #region Event Handlers - Toolbar Buttons
        private async void RecognizeButton_Click(object sender, RoutedEventArgs e)
        {
            var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            if (strokes.Count > 0)
            {
                // Disable use of toolbar during recognition and rendering
                ToggleInkToolbar();
                ToggleProgressRing();

                // Clear result canvas and viewable JSON before recognition and rendering of results
                ViewCanvasButton_Click(null, null);
                ClearJson();

                // Add any "hidden" strokes back to the canvas before recognizing
                if (hiddenStrokes.Count > 0)
                {
                    foreach (KeyValuePair<string, List<InkStroke>> item in hiddenStrokes)
                    {
                        foreach (InkStroke stroke in item.Value)
                        {
                            inkCanvas.InkPresenter.StrokeContainer.AddStroke(stroke.Clone());
                        }
                    }

                    strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
                }

                // Set language code, convert ink to JSON for request, and display it
                string languageCode = languageDropdown.SelectedValue.ToString();
                inkRecognizer.SetLanguage(languageCode);

                inkRecognizer.ClearStrokes();
                inkRecognizer.AddStrokes(strokes);
                JsonObject json = inkRecognizer.ConvertInkToJson();
                requestJson.Text = FormatJson(json.Stringify());

                // Recognize Ink from JSON and display response
                var response = await inkRecognizer.RecognizeAsync(json);
                string responseString = await response.Content.ReadAsStringAsync();
                inkResponse = JsonConvert.DeserializeObject<InkResponse>(responseString);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // Generate JSON tree view and draw result on right side canvas
                    CreateJsonTree();
                    responseJson.Text = FormatJson(responseString);

                    hiddenStrokes.Clear();
                    wordTextLayouts.Clear();
                    lineTextLayouts.Clear();
                    drawnTextLayouts.Clear();

                    var recoUnits = inkResponse.RecognitionUnits.Where(x => x.category == "inkWord" || x.category == "line");
                    foreach (var recoUnit in recoUnits)
                    {
                        CreateTextLayout(recoUnit);
                    }
                }
                else
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.NotFound)
                    {
                        await new MessageDialog("Access denied due to invalid subscription key or wrong API endpoint. Make sure to provide a valid key for an active subscription and use a correct API endpoint in the Settings page.", $"Response Code: {inkResponse.Error.code}").ShowAsync();
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
            viewDataButton.Visibility = Visibility.Visible;
        }

        private void ViewDataButton_Click(object sender, RoutedEventArgs e)
        {
            jsonPivot.Visibility = Visibility.Visible;
            viewDataButton.Visibility = Visibility.Collapsed;
            viewCanvasButton.Visibility = Visibility.Visible;
        }
        #endregion

        #region Event Handlers - Ink Canvas
        private void InkPresenter_StrokeInputStarted(InkStrokeInput sender, PointerEventArgs args)
        {
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

        //private void InkCanvas_Tapped(object sender, TappedRoutedEventArgs e)
        private void InkCanvas_Tapped(InkUnprocessedInput input, PointerEventArgs e)
        {
            if (inkResponse != null)
            {
                //var tapPosition = e.GetPosition(inkCanvas);
                var tapPosition = e.CurrentPoint.Position;
                foreach (var recoUnit in inkResponse.RecognitionUnits)
                {
                    if (recoUnit.category == "inkWord")
                    {
                        var rect = recoUnit.boundingRectangle;
                        var boundingRect = new Rect(rect.topX * dipsPerMm, rect.topY * dipsPerMm, rect.width * dipsPerMm, rect.height * dipsPerMm);
                        if (boundingRect.Contains(tapPosition))
                        {
                            if (drawnTextLayouts.ContainsKey(recoUnit.recognizedText))
                            {
                                drawnTextLayouts.Remove(recoUnit.recognizedText);

                                var resultCanvas = this.FindName("resultCanvas") as CanvasControl;
                                resultCanvas.Invalidate();

                                var strokes = hiddenStrokes[recoUnit.recognizedText];
                                foreach (var stroke in strokes)
                                {
                                    float x = (float)stroke.BoundingRect.X;
                                    float y = (float)stroke.BoundingRect.Y;
                                    var strokePosition = new Point(x, y);

                                    if (boundingRect.Contains(strokePosition))
                                    {
                                        inkCanvas.InkPresenter.StrokeContainer.AddStroke(stroke.Clone());
                                    }
                                }

                                hiddenStrokes.Remove(recoUnit.recognizedText);
                            }
                            else
                            {
                                var strokes = inkCanvas.InkPresenter.StrokeContainer.GetStrokes();
                                var hiddenStrokesList = new List<InkStroke>();
                                foreach (InkStroke stroke in strokes)
                                {
                                    float x = (float)stroke.BoundingRect.X;
                                    float y = (float)stroke.BoundingRect.Y;
                                    var strokePosition = new Point(x, y);

                                    if (boundingRect.Contains(strokePosition))
                                    {
                                        stroke.Selected = true;
                                        hiddenStrokesList.Add(stroke);
                                    }
                                }

                                hiddenStrokes.Add(recoUnit.recognizedText, hiddenStrokesList);
                                inkCanvas.InkPresenter.StrokeContainer.DeleteSelected();

                                var textLayout = wordTextLayouts[recoUnit.recognizedText];
                                drawnTextLayouts.Add(recoUnit.recognizedText, textLayout);

                                var resultCanvas = this.FindName("resultCanvas") as CanvasControl;
                                resultCanvas.Invalidate();
                            }
                        }
                    }
                }
            }
        }

        private void InkCanvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {

        }
        #endregion

        #region Draw Results
        private void ResultCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (!string.IsNullOrEmpty(responseJson.Text))
            {
                foreach (var layout in drawnTextLayouts)
                {
                    // Deconstruct tuple from drawnTextLayouts and draw the attached text layout on the result canvas
                    (CanvasTextLayout textLayout, Point point) = layout.Value;
                    args.DrawingSession.DrawTextLayout(textLayout, (float)point.X, (float)point.Y, Colors.Black);
                }
            }
            else
            {
                args.DrawingSession.Clear(Colors.White);
            }
        }
        #endregion

        #region JSON To Tree View
        private void CreateJsonTree()
        {
            // Add all of the ink recognition units that will become nodes to a collection
            foreach (var recoUnit in inkResponse.RecognitionUnits)
            {
                if (recoUnit.parentId == 0)
                {
                    recoTreeParentNodes.Add(recoUnit);
                }
                else
                {
                    recoTreeNodes.Add(recoUnit.id, recoUnit);
                }
            }

            // Add the initial "root" node for all of the ink recognition units to be added under
            string itemCount = $"{recoTreeParentNodes.Count} item{(recoTreeParentNodes.Count > 1 ? "s" : string.Empty)}";
            var root = new TreeViewNode()
            {
                Content = new KeyValuePair<string, string>("Root", itemCount)
            };

            // Traverse the list of top level parent nodes and append children if they have any
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
                else if (category == "inkDrawing")
                {
                    node.Content = new KeyValuePair<string, string>(category, parent.recognizedObject);
                }

                root.Children.Add(node);
            }

            recoTreeNodes.Clear();
            recoTreeParentNodes.Clear();

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

        private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            var node = args.InvokedItem as TreeViewNode;
            if (node.IsExpanded == false)
            {
                node.IsExpanded = true;
            }
            else
            {
                node.IsExpanded = false;
            }
        }

        private void ExpandAllButton_Click(object sender, RoutedEventArgs e)
        {
            expandAllButton.Visibility = Visibility.Collapsed;
            collapseAllButton.Visibility = Visibility.Visible;

            if (treeView.RootNodes.Count > 0)
            {
                var node = treeView.RootNodes[0];
                node.IsExpanded = true;

                ExpandChildren(node);
            }
        }

        private void CollapseAllButton_Click(object sender, RoutedEventArgs e)
        {
            collapseAllButton.Visibility = Visibility.Collapsed;
            expandAllButton.Visibility = Visibility.Visible;

            if (treeView.RootNodes.Count > 0)
            {
                var node = treeView.RootNodes[0];
                node.IsExpanded = true;

                CollapseChildren(node);
            }
        }
        #endregion

        #region Helpers
        private void ClearJson()
        {
            requestJson.Text = string.Empty;
            responseJson.Text = string.Empty;
            treeView.RootNodes.Clear();

            var resultCanvas = this.FindName("resultCanvas") as CanvasControl;
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
            if (inkToolbar.IsEnabled)
            {
                inkToolbar.IsEnabled = false;
                inkCanvas.InkPresenter.IsInputEnabled = false;

                foreach (var child in customToolbar.Children)
                {
                    var button = child as InkToolbarCustomToolButton;
                    button.IsEnabled = false;
                }
            }
            else
            {
                inkToolbar.IsEnabled = true;
                inkCanvas.InkPresenter.IsInputEnabled = true;

                foreach (var child in customToolbar.Children)
                {
                    var button = child as InkToolbarCustomToolButton;
                    button.IsEnabled = true;
                }
            }
        }

        private Color GetStrokeColor(InkRecognitionUnit recoUnit)
        {
            uint strokeId = (uint)recoUnit.strokeIds[0];
            var color = inkCanvas.InkPresenter.StrokeContainer.GetStrokeById(strokeId).DrawingAttributes.Color;

            return color;
        }

        private void CreateTextLayout(InkRecognitionUnit recoUnit)
        {
            var textFormat = new CanvasTextFormat()
            {
                WordWrapping = CanvasWordWrapping.NoWrap,
                FontFamily = "Ink Free"
            };

            if (recoUnit.category == "inkWord")
            {
                // Get height of inkWord's line instead of the word itself so all words rendered will be the same height on that line
                var layoutWordParentLine = inkResponse.RecognitionUnits.Where(x => x.id == recoUnit.parentId).Single();
                float parentLineHeight = (float)layoutWordParentLine.boundingRectangle.height * dipsPerMm;
                float parentLineY = (float)layoutWordParentLine.boundingRectangle.topY * dipsPerMm;

                textFormat.FontSize = parentLineHeight;
                string recognizedText = recoUnit.recognizedText;
                float width = (float)recoUnit.boundingRectangle.width * dipsPerMm;
                float height = parentLineHeight;
                Color color = GetStrokeColor(recoUnit);

                var resultCanvas = this.FindName("resultCanvas") as CanvasControl;
                var textLayout = new CanvasTextLayout(resultCanvas.Device, recognizedText, textFormat, width, height);
                textLayout.SetColor(0, recognizedText.Length, color);

                var point = new Point(recoUnit.boundingRectangle.topX * dipsPerMm, parentLineY);

                wordTextLayouts.TryAdd(recognizedText, new Tuple<CanvasTextLayout, Point>(textLayout, point));
            }
            else if (recoUnit.category == "line")
            {
                float boundingRectHeight = (float)recoUnit.boundingRectangle.height * dipsPerMm;
                float boundingRectX = (float)recoUnit.boundingRectangle.topX * dipsPerMm;
                float boundingRectY = (float)recoUnit.boundingRectangle.topY * dipsPerMm;

                textFormat.FontSize = boundingRectHeight;
                string recognizedText = recoUnit.recognizedText;
                float width = (float)recoUnit.boundingRectangle.width * dipsPerMm;
                float height = boundingRectHeight;

                var resultCanvas = this.FindName("resultCanvas") as CanvasControl;
                var textLayout = new CanvasTextLayout(resultCanvas.Device, recognizedText, textFormat, width, height);

                int index = 0;
                var layoutWords = inkResponse.RecognitionUnits.Where(x => recoUnit.childIds.Contains(x.id));
                foreach (var word in layoutWords)
                {
                    var color = GetStrokeColor(word);
                    textLayout.SetColor(index, word.recognizedText.Length, color);
                    index += word.recognizedText.Length + 1;
                }

                var point = new Point(boundingRectX, boundingRectY);

                lineTextLayouts.TryAdd(recognizedText, new Tuple<CanvasTextLayout, Point>(textLayout, point));
            }
        }

        private void ExpandChildren(TreeViewNode node)
        {
            if (node.HasChildren)
            {
                var children = node.Children;

                foreach (var child in children)
                {
                    child.IsExpanded = true;

                    if (child.HasChildren)
                    {
                        ExpandChildren(child);
                    }
                }
            }
        }

        private void CollapseChildren(TreeViewNode node)
        {
            if (node.HasChildren)
            {
                var children = node.Children;

                foreach (var child in children)
                {
                    child.IsExpanded = false;

                    if (child.HasChildren)
                    {
                        CollapseChildren(child);
                    }
                }
            }
        }
        #endregion
    }
}
