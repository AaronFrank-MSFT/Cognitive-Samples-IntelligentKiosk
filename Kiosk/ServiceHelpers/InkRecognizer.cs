using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Windows.Data.Json;
using Windows.UI.Input.Inking;

namespace ServiceHelpers
{
    public class InkRecognizer
    {
        IDictionary<uint, InkStroke> strokeMap = new Dictionary<uint, InkStroke>();

        public void AddStrokes(IReadOnlyList<InkStroke> strokes)
        {
            foreach (InkStroke stroke in strokes)
            {
                strokeMap[stroke.Id] = stroke;
            }
        }

        public void RemoveStroke(uint strokeId)
        {
            strokeMap.Remove(strokeId);
        }

        public JsonObject ConvertInkToJson()
        {
            const float dipsPerMm = 96 / 25.4f;
            var payload = new JsonObject();
            var strokesArray = new JsonArray();

            foreach (var stroke in strokeMap.Values)
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
            payload["language"] = JsonValue.CreateStringValue("en-US");
            payload["strokes"] = strokesArray;

            return payload;
        }
    }
}
