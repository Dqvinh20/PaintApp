using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Common;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace PaintApp
{
    public partial class MainWindow : Fluent.RibbonWindow, INotifyPropertyChanged
    {
        #region Declare variable
        // Pen
        private int _currentThickness = 1;
        private DoubleCollection _currentStrokeStyle = new DoubleCollection();
        private SolidColorBrush _currentColor = new SolidColorBrush(Colors.Black);
        private IShape _preview = null;
        private Matrix _originalMatrix;

        // Toggle var
        private bool _isDrawing = false;
        private bool _isNewDrawShape = false;
        private bool _isEditing = false;
        private bool _isChanged = false;

        private bool _isZooming = false;
        private bool _isZoomingIn = false;
        private bool _isSelectingShape;

        // Shapes prototype
        PaintShapeFactory _shapeFactoryIns = PaintShapeFactory.Instance;
        List<IShape> _loadedShapePrototypes = new List<IShape>();
        private string _selectedShapePrototypeName = "";

        // Shapes 
        Stack<IShape> _drawedShapes = new Stack<IShape>();   
        Stack<IShape> _redoShapeStack = new Stack<IShape>();

        // File path
        private String? filePath = null;
        private String? fileName = null;

        public SolidColorBrush CurrentColor { 
            get { return _currentColor; } 
            set { _currentColor = value; }
        }

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            MatrixTransform? matrixTransform = drawingArea.RenderTransform as MatrixTransform;
            if (matrixTransform != null )
            {
                _originalMatrix = matrixTransform.Matrix;
            }
        }

        #region MainWindow function

        private void PaintApp_Loaded(object sender, RoutedEventArgs e)
        {
            _loadDynamicShapePrototypes();
            DataContext = this;
            _resetToDefault();
        }

        private void PaintApp_Closing(object sender, CancelEventArgs e)
        {
            //if (_drawedShapes.Count == 0) return;
            if (_isChanged == false)
            {
                return;
            }

            if (fileName == null)
            {
                fileName = "Untitle";
            }

            String title = $"There are unsaved changes in \"{fileName}\".";

            var result = System.Windows.MessageBox.Show(title, "Do you want to save current work?", MessageBoxButton.YesNoCancel);

            if (MessageBoxResult.Yes == result)
            {
                try
                {
                    _saveFile();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            else if (MessageBoxResult.No == result)
            {

            }
            else if (MessageBoxResult.Cancel == result)
            {
                e.Cancel = true;
            }
        }

        #endregion

        #region File HeaderMenu

        private void _saveFile()
        {
            if (_drawedShapes.Count == 0) { return; }

            if (filePath == null)
            {
                var dialog = new System.Windows.Forms.SaveFileDialog();

                dialog.Filter = "BIN (*.bin)|*.bin";
                dialog.FileName = "Untitle.bin";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.FileName;
                    filePath = path;

                    FileInfo file = new FileInfo(path);
                    fileName = file.Name;

                    Title = $"Paint - {fileName}";

                    using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(path, FileMode.Create)))
                    {
                        foreach (IShape shape in _drawedShapes)
                        {
                            binaryWriter.Write(shape.Serialize());
                        }
                    }
                }
            }
            else
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(filePath, FileMode.OpenOrCreate)))
                {
                    foreach (IShape shape in _drawedShapes)
                    {
                        binaryWriter.Write(shape.Serialize());
                    }
                }
            }
            _isChanged = false;
        }

        private void createNewButton_Click(object sender, RoutedEventArgs e)
        {
            //if (_drawedShapes.Count == 0)
            if (_isChanged == false)
            {
                _resetToDefault();
                e.Handled = true;
                return;
            }

            if (fileName == null)
            {
                fileName = "Untitle";
            }

            String title = $"There are unsaved changes in \"{fileName}\".";

            var result = System.Windows.MessageBox.Show(title, "Do you want to save current work?", MessageBoxButton.YesNoCancel);

            if (MessageBoxResult.Yes == result)
            {
                saveFileButton_Click(sender, e);

                _resetToDefault();
                e.Handled = true;
            }
            else if (MessageBoxResult.No == result)
            {
                _resetToDefault();
                e.Handled = true;
            }
            else if (MessageBoxResult.Cancel == result)
            {
                e.Handled = false;
            }
        }

        private void openFileButton_Click(object sender, RoutedEventArgs e)
        {
            createNewButton_Click(sender, e);
            if (!e.Handled)
            {
                return;
            }

            System.Windows.Forms.OpenFileDialog openFile = new System.Windows.Forms.OpenFileDialog();
            openFile.Filter = "BIN (*.bin)|*.bin";

            if (openFile.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream file = File.Open(openFile.FileName, FileMode.Open);
                filePath = openFile.FileName;
                fileName = openFile.SafeFileName;
                Title = $"Paint - {fileName}";

                using (BinaryReader binaryReader = new BinaryReader(file))
                {
                    //Đọc đến khi hết file.
                    while (binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
                    {
                        string name = binaryReader.ReadString();
                        long size = binaryReader.ReadInt64();
                        byte[] data = binaryReader.ReadBytes((int)size);

                        IShape shape = _shapeFactoryIns.CreateShape(name).Deserialize(data);
                        _drawedShapes.Push(shape);
                    }

                    _redrawCanvas();
                }
                _isChanged = false;
            }
        }

        private void saveFileButton_Click(object sender, RoutedEventArgs e)
        {
            _saveFile();
            e.Handled = true;
        }

        private void importButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void saveAsPngButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.SaveFileDialog();

            dialog.Filter = "PNG (*.png)|*.png";
            dialog.FileName = "Untitle.png";

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = dialog.FileName;

                Rect rect = new Rect(drawingArea.RenderSize);
                RenderTargetBitmap renderTargetBitmap =
                    new RenderTargetBitmap((int)rect.Right, (int)rect.Bottom, 96d, 96d, PixelFormats.Default);
                renderTargetBitmap.Render(drawingArea);

                BitmapEncoder pngEncoder = new PngBitmapEncoder();
                pngEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                MemoryStream memoryStream = new MemoryStream();

                pngEncoder.Save(memoryStream);
                memoryStream.Close();

                File.WriteAllBytes(path, memoryStream.ToArray());
            }
        }

        #endregion

        private void _resetToDefault()
        {
            Title = "Paint - Untitle";
            _isChanged = false;
            _isDrawing = false;
            _isNewDrawShape = false;

            filePath = null;
            fileName = null;

            CurrentColor = new SolidColorBrush(Colors.Black);

            _updateToggleAttribute();
            _updateSelectedShapePrototype(0);

            strokeStyleComboBox.SelectedIndex = 0;
            penSizeComboBox.SelectedIndex = 0;

            _drawedShapes.Clear();
            _redoShapeStack.Clear();

            drawingArea.Children.Clear();
            drawingArea.Background = new SolidColorBrush(Colors.White);

            _updateToggleAttribute();
        }

        private void _loadDynamicShapePrototypes()
        {
            _loadedShapePrototypes = _shapeFactoryIns.GetPrototypes().Values.ToList();
            shapeListView.ItemsSource = _loadedShapePrototypes;

            if (_loadedShapePrototypes.Count == 0)
            {
                return;
            }

            shapeListView.SelectedIndex = 0;
        }

        private void _updateSelectedShapePrototype(int index)
        {
            if (index >= _loadedShapePrototypes.Count || index < 0) { return; }

            _selectedShapePrototypeName = _loadedShapePrototypes[index].Name;
            _preview = _shapeFactoryIns.CreateShape(_selectedShapePrototypeName);
        }

        private void _updateToggleAttribute()
        {
            redoButton.IsEnabled = _redoShapeStack.Count > 0;
            undoButton.IsEnabled = _drawedShapes.Count > 0;
        }

        private void onShape_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var index = shapeListView.SelectedIndex;
            _updateSelectedShapePrototype(index);
        }

        #region QuickAccessItems
        private void onUndoButtonClick(object sender, RoutedEventArgs e)
        {
            IShape topStack;
            bool isPopable = _drawedShapes.TryPop(out topStack);
            if (isPopable)
            {
                _redoShapeStack.Push(topStack);

                int lastShapeIndex = drawingArea.Children.Count - 1;
                drawingArea.Children.RemoveAt(lastShapeIndex);
                _isChanged = true;

                _updateToggleAttribute();
            }
        }

        private void onRedoButtonClick(object sender, RoutedEventArgs e)
        {
            IShape topStack;
            bool isPopable = _redoShapeStack.TryPop(out topStack);
            if (isPopable)
            {
                _drawedShapes.Push(topStack);
                drawingArea.Children.Add(topStack.Draw());
                _isChanged = true;

                _updateToggleAttribute();
            }
        }
        #endregion

        #region Clipboard copy paste cut
        private void onPaste(object sender, RoutedEventArgs e)
        {

        }

        private void onCopy(object sender, RoutedEventArgs e)
        {

        }

        private void onCut(object sender, RoutedEventArgs e)
        {

        }

        #endregion

        #region StrokeStyle
        private void onStrokeStyleChanged(object sender, SelectionChangedEventArgs e)
        {
            switch(strokeStyleComboBox.SelectedIndex)
            {
                case 0:
                    _currentStrokeStyle = new DoubleCollection();
                    break;
                case 1:
                    _currentStrokeStyle = new DoubleCollection() { 4, 1, 1, 1, 1, 1 };
                    break;
                case 2:
                    _currentStrokeStyle = new DoubleCollection() { 1, 1 };
                    break;
                case 3:
                    _currentStrokeStyle = new DoubleCollection() { 6, 1 };
                    break;
                default:
                    _currentStrokeStyle = new DoubleCollection();
                    break;
            }
        }
        #endregion

        #region Pen Size
        private void onPenSizeChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (penSizeComboBox.SelectedIndex)
            {
                case 0:
                    _currentThickness = 1;
                    break;
                case 1:
                    _currentThickness = 3;
                    break;
                case 2:
                    _currentThickness = 5;
                    break;
                case 3:
                    _currentThickness = 8;
                    break;
                default:
                    _currentThickness = 1;
                    break;
            }
        }
        #endregion

        #region Color change pen color
        private void onEditColor_ButtonClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.ColorDialog colorPicker = new System.Windows.Forms.ColorDialog();

            if (colorPicker.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CurrentColor = new SolidColorBrush(Color.FromRgb(colorPicker.Color.R, colorPicker.Color.G, colorPicker.Color.B));
            }
        }

        private void onChangePenColor(object sender, RoutedEventArgs e)
        {
            RadioButton radioButton = (RadioButton)sender;
            CurrentColor = (SolidColorBrush)radioButton.Background;
        }
        #endregion

        #region Canvas draw method
        private void _drawShape(UIElement uIElement, bool isRemoveEnd)
        {
            if (isRemoveEnd && drawingArea.Children.Count > 0)
            {
                // Remove last item
                drawingArea.Children.RemoveAt((drawingArea.Children.Count - 1));
            }
            drawingArea.Children.Add(uIElement);
        }
        private void _redrawCanvas()
        {
            foreach (var shape in _drawedShapes)
            {
                drawingArea.Children.Add(shape.Draw());
            }
        }

        #endregion

        #region Canvas Mouse event handlers
        private void onCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = true;
            _isNewDrawShape = true;
            _preview.HandleStart(e.GetPosition(drawingArea));
        }

        private void onCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawing)
            {
                if (_redoShapeStack.Count != 0) _redoShapeStack.Clear();

                _preview.HandleEnd(e.GetPosition(drawingArea));
                UIElement uIElement = _preview.Draw(_currentColor, _currentThickness, _currentStrokeStyle);
                if (_isNewDrawShape)
                {
                    _drawShape(uIElement, false);
                    _isNewDrawShape = false;
                }
                else
                {
                    _drawShape(uIElement, true);
                }
            }
        }

        private void onCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawing)
            {
                _isChanged = true;
                _isDrawing = false;
                _preview.HandleEnd(e.GetPosition(drawingArea));
                _drawedShapes.Push(_preview); 

                // Create new shape
                _preview = _shapeFactoryIns.CreateShape(_selectedShapePrototypeName);
            }

            _updateToggleAttribute();
        }
        #endregion

        #region Zoom Mode ZoomIn ZoomOut ZoomRestore100%
        private void onZoom_ToggleButton(object sender, RoutedEventArgs e)
        {
            //if (sender is Fluent.ToggleButton)
            //{
            //    Fluent.ToggleButton button = (Fluent.ToggleButton)sender;

            //    _isZooming = (bool)zoomInButton.IsChecked || (bool)zoomOutButton.IsChecked;
            //    _isZoomingIn = button.Name.Equals("zoomInButton") && (bool)zoomInButton.IsChecked;
            //    zoomInButton.IsChecked = _isZooming && _isZoomingIn;
            //    zoomOutButton.IsChecked = _isZooming && !_isZoomingIn;
            //}
            //else
            //{
            //    zoomInButton.IsChecked = false;
            //    zoomOutButton.IsChecked = false;
            //    _isZooming = false;
            //    _onZoomRestore();
            //}
        }

        private void _onZoomRestore()
        {
            //var currMatrixTransform = drawingArea.RenderTransform as MatrixTransform;
            //currMatrixTransform.Matrix = _originalMatrix;
        }

        private void _onZoomIn(Point point)
        {
            Point center = drawingArea.TransformToAncestor(drawingContainer).Transform(point);

            var matTrans = drawingArea.RenderTransform as MatrixTransform;
            var mat = matTrans.Matrix;
            var scale = 1.1;
            mat.ScaleAt(scale, scale, center.X, center.Y);
            matTrans.Matrix = mat;
        }

        private void _onZoomOut(Point point)
        {
            Point point1 = new Point(drawingArea.ActualWidth / 2, drawingArea.ActualHeight / 2);
            Point center = drawingArea.TransformToAncestor(drawingContainer).Transform(point);

            var matTrans = drawingArea.RenderTransform as MatrixTransform;
            var mat = matTrans.Matrix;
            var scale = 1 / 1.1;
            mat.ScaleAt(scale, scale, center.X, center.Y);
            matTrans.Matrix = mat;
        }

        private void onMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            //var matTrans = drawingArea.RenderTransform as MatrixTransform;
            //var pos1 = e.GetPosition(drawingArea);

            //var scale = e.Delta > 0 ? 1.1 : 1 / 1.1;

            //var mat = matTrans.Matrix;
            //mat.ScaleAt(scale, scale, pos1.X, pos1.Y);
            //matTrans.Matrix = mat;
            //e.Handled = true;
        }

        #endregion

        #region ToggleButton
        private void onChange_ToggleButton(object sender, RoutedEventArgs e)
        {
            Fluent.ToggleButton toggle = (Fluent.ToggleButton)sender;

            switch(toggle.Name)
            {
                case "editModeButton":
                    _isEditing = (bool) toggle.IsChecked;
                    break;
                case "fillColorButton":
                    break;
                case "1":
                    break;
                case "2":
                    break;
            }

        }

        #endregion

        private void onDelete(object sender, RoutedEventArgs e)
        {

        }
    }
}
