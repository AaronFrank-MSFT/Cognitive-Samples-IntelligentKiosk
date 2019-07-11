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

using IntelligentKioskSample.Models.InkRecognizerExplorer;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace IntelligentKioskSample.Controls
{
    public sealed partial class InkRecoUnitControl : UserControl
    {
        private InkRecoUnitControl parent;
        private Polyline lineToParent;
        private Windows.Foundation.Point childConnection;
        private Windows.Foundation.Point parentConnection;

        public InkRecoUnitControl()
        {
            this.InitializeComponent();

            this.Width = 110;
            this.Height = 30;

            childConnection = new Windows.Foundation.Point(this.Width / 2.0f, 0);
            parentConnection = new Windows.Foundation.Point(this.Width / 2.0f, this.Height);
        }

        public InkRecoUnitControl(InkRecoUnitControl parent, InkRecognitionUnit recoUnit) : this()
        {
            this.parent = parent;
            recoText.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center;
            this.lineToParent = new Polyline();
            if (this.parent != null)
            {
                this.lineToParent.Points.Add(this.parent.ParentConnection);
                this.lineToParent.Points.Add(this.childConnection);
            }


            if (recoUnit == null)
            {
                recoText.Text = "Root";
                return;
            }

            if (!string.IsNullOrEmpty(recoUnit.recognizedObject))
            {
                recoText.Text = recoUnit.recognizedObject;
            }
            else if (!string.IsNullOrEmpty(recoUnit.recognizedText))
            {
                recoText.Text = recoUnit.recognizedText;
            }
            else
            {
                recoText.Text = recoUnit.@class == "container" ? recoUnit.category : "No results!";
            }
        }

        public Windows.Foundation.Point ChildConnection { get => childConnection; }

        public Windows.Foundation.Point ParentConnection { get => parentConnection; }
    }
}
