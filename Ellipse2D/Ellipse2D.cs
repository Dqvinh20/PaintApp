using Common;
using System;
using System.IO;
using System.Security.Policy;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Ellipse2D
{
    public class Ellipse2D : IShape
    {
        public string Name => "Ellipse";
        public string Icon => "Images/icons8-ellipse-26.png";

        public SolidColorBrush Brush { get; set; }
        public int Thickness { get; set; }
        public DoubleCollection Stroke { get; set; }

        private Point2D _topLeft = new Point2D();
        private Point2D _bottomRight = new Point2D();

        public IShape Clone()
        {
            return new Ellipse2D();
        }

        public UIElement Draw(SolidColorBrush brush, double thickness, DoubleCollection stroke)
        {
            this.Brush = brush;
            this.Stroke = stroke;
            this.Thickness = (int)thickness;

            return Draw();
        }

        public UIElement Draw()
        {
            var left = Math.Min(_topLeft.X, _bottomRight.X);
            var top = Math.Min(_topLeft.Y, _bottomRight.Y);

            var right = Math.Max(_topLeft.X, _bottomRight.X);
            var bottom = Math.Max(_topLeft.Y, _bottomRight.Y);

            var width = right - left;
            var height = bottom - top;

            var ellipse = new Ellipse()
            {
                Width = width,
                Height = height,
                Stroke = Brush,
                StrokeThickness = Thickness,
                StrokeDashArray = Stroke
            };

            Canvas.SetLeft(ellipse, left);
            Canvas.SetTop(ellipse, top);

            return ellipse;
        }

        public void HandleEnd(double x, double y)
        {
            _bottomRight = new Point2D() { X = x, Y = y };
        }

        public void HandleStart(double x, double y)
        {
            _topLeft = new Point2D() { X = x, Y = y };
        }

        public byte[] Serialize()
        {
            using (MemoryStream data = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(data))
                {
                    writer.Write(_topLeft.Serialize());
                    writer.Write(_bottomRight.Serialize());
                    writer.Write(Brush.ToString());
                    writer.Write(Thickness);
                    writer.Write(Stroke.ToString());

                    using (MemoryStream content = new MemoryStream())
                    {
                        using (BinaryWriter writer1 = new BinaryWriter(content))
                        {
                            writer1.Write(Name);
                            writer1.Write(data.Length);
                            writer1.Write(data.ToArray());

                            return content.ToArray();
                        }
                    }
                }
            }
        }

        public IShape Deserialize(byte[] data)
        {
            Ellipse2D result = new Ellipse2D();
            using (MemoryStream dataStream = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(dataStream))
                {
                    reader.ReadString(); // Read name point
                    long sizeStart = reader.ReadInt64();
                    result._topLeft = result._topLeft.Deserialize(reader.ReadBytes((int)sizeStart)) as Point2D;

                    reader.ReadString(); // Read name point
                    long sizeEnd = reader.ReadInt64();
                    result._bottomRight = result._bottomRight.Deserialize(reader.ReadBytes((int)sizeEnd)) as Point2D;

                    BrushConverter brushConverter = new BrushConverter();
                    result.Brush = brushConverter.ConvertFromString(reader.ReadString()) as SolidColorBrush;

                    result.Thickness = reader.ReadInt32();

                    DoubleCollectionConverter converter = new DoubleCollectionConverter();
                    result.Stroke = converter.ConvertFromString(reader.ReadString()) as DoubleCollection;

                    return result;
                }
            }
        }
    }
}
