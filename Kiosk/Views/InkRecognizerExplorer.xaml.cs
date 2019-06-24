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

using Microsoft.Graphics.Canvas.UI.Xaml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServiceHelpers;
using System;
using System.Collections.Generic;
using System.Net.Http;
using Windows.Data.Json;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace IntelligentKioskSample.Views
{
    [KioskExperience(Title = "Ink Recognizer Explorer", ImagePath = "ms-appx:/Assets/TranslatorExplorer.png")]
    public sealed partial class InkRecognizerExplorer : Page
    {
        string subscriptionKey = SettingsHelper.Instance.InkRecognizerApiKey;
        const string endpoint = "https://api.cognitive.microsoft.com";
        const string inkRecognitionUrl = "/inkrecognizer/v1.0-preview/recognize";

        ServiceHelpers.InkRecognizer inkRecognizer;

        // Timer to be used to trigger ink recognition
        private readonly DispatcherTimer dispatcherTimer;
        const double IDLE_TIME = 500;

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
                    // Convert Ink to JSON for request and display it
                    JsonObject json = inkRecognizer.ConvertInkToJson();
                    requestJson.Text = inkRecognizer.FormatJson(json.Stringify());

                    // Recognize Ink from JSON and display response
                    var response = await inkRecognizer.RecognizeAsync(json);
                    string responseString = await response.Content.ReadAsStringAsync();
                    responseJson.Text = inkRecognizer.FormatJson(responseString);

                    // Draw result on right side canvas
                    resultCanvas.Invalidate();
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

        private void ResultCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (!(responseJson.Text == string.Empty))
            {
                args.DrawingSession.DrawText("it works", 10, 10, Colors.Black);
            }
            else
            {
                args.DrawingSession.Clear(Colors.White);
            }
        }
    }
}
