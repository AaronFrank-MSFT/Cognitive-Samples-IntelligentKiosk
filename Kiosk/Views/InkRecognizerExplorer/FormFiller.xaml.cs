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
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace IntelligentKioskSample.Views.InkRecognizerExplorer
{
    public sealed partial class FormFiller : Page
    {
        string subscriptionKey = SettingsHelper.Instance.InkRecognizerApiKey;
        string endpoint = SettingsHelper.Instance.InkRecognizerApiKeyEndpoint;
        const string inkRecognitionUrl = "/inkrecognizer/v1.0-preview/recognize";

        ServiceHelpers.InkRecognizer inkRecognizer;
        InkResponse inkResponse;

        private Symbol TouchWriting = (Symbol)0xED5F;
        private Symbol Accept = (Symbol)0xE8FB;
        private Symbol Undo = (Symbol)0xE7A7;
        private Symbol Redo = (Symbol)0xE7A6;
        private Symbol ClearAll = (Symbol)0xE74D;

        InkCanvas currentCanvas;

        public FormFiller()
        {
            this.InitializeComponent();

            inkRecognizer = new ServiceHelpers.InkRecognizer(subscriptionKey, endpoint, inkRecognitionUrl);
        }

        private void ExpandAllButton_Click(object sender, RoutedEventArgs e)
        {
            expandAllButton.Visibility = Visibility.Collapsed;
            collapseAllButton.Visibility = Visibility.Visible;

            foreach (var child in formFields.Children)
            {
                var element = child as Expander;
                element.IsExpanded = true;
            }
        }

        private void CollapseAllButton_Click(object sender, RoutedEventArgs e)
        {
            collapseAllButton.Visibility = Visibility.Collapsed;
            expandAllButton.Visibility = Visibility.Visible;

            foreach (var child in formFields.Children)
            {
                var element = child as Expander;
                element.IsExpanded = false;
            }
        }

        private void InkCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            currentCanvas = sender as InkCanvas;
        }

        private void InkToolbarCustomToolButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as InkToolbarCustomToolButton;
            button.IsChecked = false;
        }
    }
}
