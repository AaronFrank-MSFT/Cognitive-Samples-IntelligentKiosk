﻿// 
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
using Microsoft.Toolkit.Uwp.UI.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using Windows.Data.Json;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace IntelligentKioskSample.Views.InkRecognizerExplorer
{
    public sealed partial class FormFiller : Page
    {
        string subscriptionKey = SettingsHelper.Instance.InkRecognizerApiKey;
        string endpoint = SettingsHelper.Instance.InkRecognizerApiKeyEndpoint;
        const string inkRecognitionUrl = "/inkrecognizer/v1.0-preview/recognize";

        private readonly DispatcherTimer dispatcherTimer;

        ServiceHelpers.InkRecognizer inkRecognizer;
        InkResponse inkResponse;
        InkCanvas currentCanvas;

        private string[] prefixes = new string[]
        {
            "year",
            "make",
            "model",
            "mileage",
            "license",
            "date",
            "time",
            "damage"
        };

        private Symbol TouchWriting = (Symbol)0xED5F;
        private Symbol Accept = (Symbol)0xE8FB;
        private Symbol Undo = (Symbol)0xE7A7;
        private Symbol Redo = (Symbol)0xE7A6;
        private Symbol ClearAll = (Symbol)0xE74D;
        
        public FormFiller()
        {
            this.InitializeComponent();

            foreach (string prefix in prefixes)
            {
                var canvas = this.FindName($"{prefix}Canvas") as InkCanvas;
                canvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Mouse;
                canvas.InkPresenter.StrokeInput.StrokeStarted += InkPresenter_StrokeInputStarted;
                canvas.InkPresenter.StrokeInput.StrokeEnded += InkPresenter_StrokeInputEnded;
                canvas.InkPresenter.StrokesErased += InkPresenter_StrokeErased;
            }

            inkRecognizer = new ServiceHelpers.InkRecognizer(subscriptionKey, endpoint, inkRecognitionUrl);

            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(350);
        }

        private void InkPresenter_StrokeInputStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            dispatcherTimer.Stop();
        }

        private void InkPresenter_StrokeInputEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            dispatcherTimer.Start();
        }

        private void InkPresenter_StrokeErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            dispatcherTimer.Start();

            var strokes = currentCanvas.InkPresenter.StrokeContainer.GetStrokes();
            if (strokes.Count == 0)
            {
                int index = currentCanvas.Name.IndexOf("Canvas");
                string prefix = currentCanvas.Name.Substring(0, index);

                var result = this.FindName($"{prefix}Result") as TextBlock;
                result.Text = string.Empty;
            }
        }

        private async void DispatcherTimer_Tick(object sender, object e)
        {
            dispatcherTimer.Stop();

            int index = currentCanvas.Name.IndexOf("Canvas");
            string prefix = currentCanvas.Name.Substring(0, index);

            var strokes = currentCanvas.InkPresenter.StrokeContainer.GetStrokes();
            inkRecognizer.ClearStrokes();
            inkRecognizer.AddStrokes(strokes);
            JsonObject json = inkRecognizer.ConvertInkToJson();

            // Recognize Ink from JSON and display response
            var response = await inkRecognizer.RecognizeAsync(json);
            string responseString = await response.Content.ReadAsStringAsync();
            inkResponse = JsonConvert.DeserializeObject<InkResponse>(responseString);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = this.FindName($"{prefix}Result") as TextBlock;
                result.Text = string.Empty;

                foreach (var recoUnit in inkResponse.RecognitionUnits)
                {
                    if (recoUnit.category == "line")
                    {
                        result.Text += $"{recoUnit.recognizedText} ";
                    }
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
        }

        private void FormField_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var formField = sender as Grid;
            string prefix = formField.Name;
            var canvasGrid = this.FindName($"{prefix}Grid") as Grid;

            if (canvasGrid.Visibility == Visibility.Collapsed)
            {
                CollapseAllFormFields();
                canvasGrid.Visibility = Visibility.Visible;

                if (formField.Tag.ToString() == "accepted")
                {
                    formField.BorderBrush = new SolidColorBrush(Colors.Yellow);
                    formField.Tag = "pending";
                }
                else
                {
                    formField.BorderThickness = new Thickness(3);
                }

                var canvas = this.FindName($"{prefix}Canvas") as InkCanvas;
                inkToolbar.TargetInkCanvas = canvas;
                currentCanvas = canvas;
            }
            else
            {
                formField.BorderThickness = new Thickness(0);
                canvasGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void CollapseAllFormFields()
        {
            foreach (string prefix in prefixes)
            {
                var formField = this.FindName($"{prefix}") as Grid;
                var canvasGrid = this.FindName($"{prefix}Grid") as Grid;

                canvasGrid.Visibility = Visibility.Collapsed;

                if (formField.Tag.ToString() == "pending")
                {
                    formField.BorderThickness = new Thickness(0);
                }
            }
        }

        private void NavigateToNextField(string prefix)
        {
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (prefixes[i] == prefix && i != (prefixes.Length - 1))
                {
                    string nextFieldPrefix = prefixes[i + 1];
                    var nextFormField = this.FindName($"{nextFieldPrefix}") as Grid;

                    if (nextFormField.Tag.ToString() == "pending")
                    {
                        ActivateFormField(nextFieldPrefix);
                    }
                    else if (nextFormField.Tag.ToString() == "accepted")
                    {
                        prefix = nextFieldPrefix;
                    }
                }
                else if (prefixes[i] == prefix && i == (prefixes.Length - 1))
                {
                    var nextFormField = this.FindName($"{prefix}") as Grid;
                    if (nextFormField.Tag.ToString() == "accepted")
                    {
                        foreach (string fieldPrefix in prefixes)
                        {
                            var formField = this.FindName($"{fieldPrefix}") as Grid;
                            if (formField.Tag.ToString() == "pending")
                            {
                                ActivateFormField(fieldPrefix);
                                return;
                            }
                        }
                    }
                }
            }
        }

        private void ActivateFormField(string prefix)
        {
            var formField = this.FindName($"{prefix}") as Grid;
            var canvasGrid = this.FindName($"{prefix}Grid") as Grid;
            var canvas = this.FindName($"{prefix}Canvas") as InkCanvas;

            formField.BorderThickness = new Thickness(3);
            canvasGrid.Visibility = Visibility.Visible;
            inkToolbar.TargetInkCanvas = canvas;
            currentCanvas = canvas;
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as InkToolbarCustomToolButton;
            button.IsChecked = false;

            int index = currentCanvas.Name.IndexOf("Canvas");
            string prefix = currentCanvas.Name.Substring(0, index);

            var formField = this.FindName($"{prefix}") as Grid;

            formField.BorderBrush = new SolidColorBrush(Colors.LightGreen);
            formField.Tag = "accepted";
            CollapseAllFormFields();
            NavigateToNextField(prefix);
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as InkToolbarCustomToolButton;
            button.IsChecked = false;

            int index = currentCanvas.Name.IndexOf("Canvas");
            string prefix = currentCanvas.Name.Substring(0, index);

        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as InkToolbarCustomToolButton;
            button.IsChecked = false;

            int index = currentCanvas.Name.IndexOf("Canvas");
            string prefix = currentCanvas.Name.Substring(0, index);
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as InkToolbarCustomToolButton;
            button.IsChecked = false;

            int index = currentCanvas.Name.IndexOf("Canvas");
            string prefix = currentCanvas.Name.Substring(0, index);
        }
    }
}