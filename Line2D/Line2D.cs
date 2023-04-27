using Common;
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Line2D
{
    public class Line2D : IShape
    {
        public string Name => "Line";
        public string Icon => "Images/icons8-line-26.png";

        public SolidColorBrush Brush { get ; set; }
        public int Thickness { get; set ; }
        public DoubleCollection Stroke { get; set; }

        private Point2D _start = new Point2D();
        private Point2D _end = new Point2D();

        public IShape Clone()
        {
            return new Line2D();
        }

        public UIElement Draw(SolidColorBrush brush, double thickness, DoubleCollection stroke)
        {
            this.Brush = brush;
            this.Stroke = stroke;
            this.Thickness = (int) thickness;

            return Draw();
        }

        public UIElement Draw()
        {
            Line line = new Line()
            {
                X1 = _start.X,
                Y1 = _start.Y,
                X2 = _end.X,
                Y2 = _end.Y,
                StrokeThickness = Thickness,
                Stroke = Brush,
                StrokeDashArray = Stroke
            };
            return line;
        }

        public void HandleEnd(double x, double y)
        {
            _end.X = x;
            _end.Y = y;
        }

        public void HandleStart(double x, double y)
        {
            _start.X = x;
            _start.Y = y;
        }

        public byte[] Serialize()
        {
            try
            {
                using (MemoryStream data = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(data))
                    {
                        writer.Write(_start.Serialize());
                        writer.Write(_end.Serialize());
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
            return null;
        }

        public IShape Deserialize(byte[] data)
        {
            Line2D result = new Line2D();
            using (MemoryStream dataStream = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(dataStream))
                {
                    reader.ReadString(); // Read name point
                    long sizeStart = reader.ReadInt64();
                    result._start = result._start.Deserialize(reader.ReadBytes((int)sizeStart)) as Point2D;

                    reader.ReadString(); // Read name point
                    long sizeEnd = reader.ReadInt64();
                    result._end = result._start.Deserialize(reader.ReadBytes((int)sizeEnd)) as Point2D;

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
