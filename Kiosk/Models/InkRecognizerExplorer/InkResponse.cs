using System.Collections.Generic;

namespace IntelligentKioskSample.Models.InkRecognizerExplorer
{
    public class InkResponse
    {
        public List<InkRecognitionUnit> RecognitionUnits { get; set; }
        public Error Error { get; set; }
    }

    public class Error
    {
        public string code { get; set; }
        public string message { get; set; }
    }
}
