using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using PizarraPro.Models;
using PizarraPro.Services;
using PizarraPro.ViewModels;

namespace PizarraPro;

public partial class MainWindow : Window
{
    private static readonly Color[] Palette =
    {
        Colors.Black, Colors.DimGray, Colors.White, Colors.Red, Colors.OrangeRed,
        Colors.Orange, Colors.Gold, Colors.Green, Colors.Teal, Colors.DodgerBlue,
        Colors.Blue, Colors.Purple, Colors.Magenta, Colors.Brown
    };

    public MainViewModel ViewModel { get; }

    private readonly Dictionary<LayerViewModel, InkCanvas> _layerCanvases = new();
    private readonly Dictionary<TextElementViewModel, TextBlock> _textVisuals = new();
    private readonly HashSet<LayerViewModel> _wiredLayers = new();

    private PageViewModel? _observedPage;

    private bool _isPanning;
    private Point _panLastScreenPoint;

    private bool _isDrawingShape;
    private ToolType _drawingTool;
    private Point _shapeStart;
    private Shape? _previewShape;

    private TextBox? _activeTextBox;
    private Point _textBoxOrigin;

    private TextElementViewModel? _selectedTextElement;
    private bool _isDraggingText;
    private Point _dragStartMouse;
    private Point _dragStartElement;

    public MainWindow()
    {
        InitializeComponent();

        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        BuildColorPalette();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        PreviewKeyDown += Window_PreviewKeyDown;

        Loaded += (_, _) =>
        {
            _observedPage = ViewModel.CurrentPage;
            if (_observedPage is not null)
            {
                _observedPage.Layers.CollectionChanged += ObservedPage_LayersChanged;
                _observedPage.PropertyChanged += ObservedPage_PropertyChanged;
            }

            RebuildLayersHost();
            FitPageToView();
        };
    }

    #region Paleta de colores

    private void BuildColorPalette()
    {
        foreach (var color in Palette)
        {
            var btn = new Button
            {
                Width = 22,
                Height = 22,
                Margin = new Thickness(2),
                Background = new SolidColorBrush(color),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Tag = color
            };
            btn.Click += (s, _) =>
            {
                if (((Button)s!).Tag is Color c) ViewModel.StrokeColor = c;
            };
            PaletteHost.Children.Add(btn);
        }
    }

    private void MoreColors_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dlg.Color;
            ViewModel.StrokeColor = Color.FromArgb(c.A, c.R, c.G, c.B);
        }
    }

    #endregion

    #region Sincronización de página / capas

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            if (_observedPage is not null)
            {
                _observedPage.Layers.CollectionChanged -= ObservedPage_LayersChanged;
                _observedPage.PropertyChanged -= ObservedPage_PropertyChanged;
            }

            _observedPage = ViewModel.CurrentPage;

            if (_observedPage is not null)
            {
                _observedPage.Layers.CollectionChanged += ObservedPage_LayersChanged;
                _observedPage.PropertyChanged += ObservedPage_PropertyChanged;
            }

            RebuildLayersHost();
            Dispatcher.BeginInvoke(new Action(FitPageToView), DispatcherPriority.Loaded);
        }
        else if (e.PropertyName is nameof(MainViewModel.CurrentTool)
                 or nameof(MainViewModel.StrokeColor)
                 or nameof(MainViewModel.StrokeThickness))
        {
            UpdateActiveLayerEditingMode();
        }
    }

    private void ObservedPage_LayersChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildLayersHost();

    private void ObservedPage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PageViewModel.ActiveLayer)) UpdateActiveLayerEditingMode();
    }

    private void RebuildLayersHost()
    {
        LayersHost.Children.Clear();
        _layerCanvases.Clear();
        _textVisuals.Clear();
        _selectedTextElement = null;

        var page = ViewModel.CurrentPage;
        if (page is null) return;

        foreach (var layer in page.Layers)
        {
            var ink = new InkCanvas
            {
                Width = page.Width,
                Height = page.Height,
                Background = Brushes.Transparent,
                Opacity = layer.Opacity,
                Visibility = layer.IsVisible ? Visibility.Visible : Visibility.Collapsed,
                Strokes = layer.Strokes,
                EditingMode = InkCanvasEditingMode.None
            };

            LayersHost.Children.Add(ink);
            _layerCanvases[layer] = ink;

            foreach (var element in layer.TextElements)
            {
                AddTextVisual(layer, element);
            }

            if (_wiredLayers.Add(layer))
            {
                layer.PropertyChanged += (_, e) => OnLayerPropertyChanged(layer, e);
                layer.TextElements.CollectionChanged += (_, e) => OnLayerTextElementsChanged(layer, e);
            }
        }

        UpdateActiveLayerEditingMode();
    }

    private void OnLayerPropertyChanged(LayerViewModel layer, PropertyChangedEventArgs e)
    {
        if (!_layerCanvases.TryGetValue(layer, out var ink)) return;

        switch (e.PropertyName)
        {
            case nameof(LayerViewModel.Opacity):
                ink.Opacity = layer.Opacity;
                break;
            case nameof(LayerViewModel.IsVisible):
                ink.Visibility = layer.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                UpdateActiveLayerEditingMode();
                break;
            case nameof(LayerViewModel.IsLocked):
                UpdateActiveLayerEditingMode();
                break;
        }
    }

    private void OnLayerTextElementsChanged(LayerViewModel layer, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TextElementViewModel item in e.OldItems) RemoveTextVisual(item);
        }

        if (e.NewItems is not null)
        {
            foreach (TextElementViewModel item in e.NewItems) AddTextVisual(layer, item);
        }
    }

    private void UpdateActiveLayerEditingMode()
    {
        var page = ViewModel.CurrentPage;
        if (page is null) return;

        foreach (var kvp in _layerCanvases)
        {
            var layer = kvp.Key;
            var ink = kvp.Value;
            var isActive = ReferenceEquals(layer, page.ActiveLayer);
            ink.IsHitTestVisible = isActive && layer.IsVisible && !layer.IsLocked;

            if (!isActive || layer.IsLocked || !layer.IsVisible)
            {
                ink.EditingMode = InkCanvasEditingMode.None;
                continue;
            }

            ink.DefaultDrawingAttributes = BuildDrawingAttributes();
            ink.EditingMode = ViewModel.CurrentTool switch
            {
                ToolType.Pen => InkCanvasEditingMode.Ink,
                ToolType.Highlighter => InkCanvasEditingMode.Ink,
                ToolType.Eraser => InkCanvasEditingMode.EraseByStroke,
                ToolType.Select => InkCanvasEditingMode.Select,
                _ => InkCanvasEditingMode.None
            };
        }
    }

    private DrawingAttributes BuildDrawingAttributes()
    {
        var da = new DrawingAttributes
        {
            Color = ViewModel.StrokeColor,
            Width = ViewModel.StrokeThickness,
            Height = ViewModel.StrokeThickness,
            FitToCurve = true,
            StylusTip = StylusTip.Ellipse
        };

        if (ViewModel.CurrentTool == ToolType.Highlighter)
        {
            da.IsHighlighter = true;
            da.Width = Math.Max(ViewModel.StrokeThickness * 3, 12);
            da.Height = da.Width * 0.6;
        }

        return da;
    }

    #endregion

    #region Elementos de texto

    private void AddTextVisual(LayerViewModel layer, TextElementViewModel element)
    {
        if (!_layerCanvases.TryGetValue(layer, out var ink)) return;

        var block = new TextBlock
        {
            Text = element.Text,
            FontSize = element.FontSize,
            Foreground = new SolidColorBrush(element.Color),
            Cursor = Cursors.SizeAll
        };
        InkCanvas.SetLeft(block, element.X);
        InkCanvas.SetTop(block, element.Y);

        block.MouseLeftButtonDown += (_, e) => TextVisual_MouseLeftButtonDown(layer, element, block, e);
        block.MouseLeftButtonUp += (s, e) => TextVisual_MouseLeftButtonUp(s, e);
        block.MouseMove += (_, e) => TextVisual_MouseMove(element, block, e);

        ink.Children.Add(block);
        _textVisuals[element] = block;
    }

    private void RemoveTextVisual(TextElementViewModel element)
    {
        if (_textVisuals.TryGetValue(element, out var block))
        {
            (block.Parent as InkCanvas)?.Children.Remove(block);
            _textVisuals.Remove(element);
        }

        if (ReferenceEquals(_selectedTextElement, element)) _selectedTextElement = null;
    }

    private void BeginTextEntry(Point boardPoint)
    {
        CommitPendingText();

        var tb = new TextBox
        {
            MinWidth = 80,
            FontSize = ViewModel.TextFontSize,
            Foreground = new SolidColorBrush(ViewModel.StrokeColor),
            Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            AcceptsReturn = false
        };
        Canvas.SetLeft(tb, boardPoint.X);
        Canvas.SetTop(tb, boardPoint.Y);

        OverlayCanvas.IsHitTestVisible = true;
        OverlayCanvas.Children.Add(tb);
        _activeTextBox = tb;
        _textBoxOrigin = boardPoint;

        tb.KeyDown += TextBoxEditor_KeyDown;
        tb.LostFocus += TextBoxEditor_LostFocus;
        tb.Focus();
    }

    private void TextBoxEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CommitPendingText(); e.Handled = true; }
        else if (e.Key == Key.Escape) { CancelPendingText(); e.Handled = true; }
    }

    private void TextBoxEditor_LostFocus(object sender, RoutedEventArgs e) => CommitPendingText();

    private void CommitPendingText()
    {
        if (_activeTextBox is null) return;
        var tb = _activeTextBox;
        _activeTextBox = null;
        tb.KeyDown -= TextBoxEditor_KeyDown;
        tb.LostFocus -= TextBoxEditor_LostFocus;
        OverlayCanvas.Children.Remove(tb);
        OverlayCanvas.IsHitTestVisible = false;

        var text = tb.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        var layer = ViewModel.CurrentPage?.ActiveLayer;
        if (layer is null || layer.IsLocked || !layer.IsVisible) return;

        var element = new TextElementViewModel
        {
            Text = text,
            X = _textBoxOrigin.X,
            Y = _textBoxOrigin.Y,
            FontSize = ViewModel.TextFontSize,
            Color = ViewModel.StrokeColor
        };
        layer.TextElements.Add(element);
        ViewModel.Undo.Push(new TextElementAddedAction(layer.TextElements, element));
        ViewModel.IsDirty = true;
    }

    private void CancelPendingText()
    {
        if (_activeTextBox is null) return;
        var tb = _activeTextBox;
        _activeTextBox = null;
        tb.KeyDown -= TextBoxEditor_KeyDown;
        tb.LostFocus -= TextBoxEditor_LostFocus;
        OverlayCanvas.Children.Remove(tb);
        OverlayCanvas.IsHitTestVisible = false;
    }

    private void TextVisual_MouseLeftButtonDown(LayerViewModel layer, TextElementViewModel element, TextBlock block, MouseButtonEventArgs e)
    {
        if (ViewModel.CurrentTool != ToolType.Select) return;

        if (e.ClickCount >= 2)
        {
            e.Handled = true;
            BeginTextEdit(layer, element, block);
            return;
        }

        _selectedTextElement = element;
        _isDraggingText = true;
        _dragStartMouse = e.GetPosition(PageSurface);
        _dragStartElement = new Point(element.X, element.Y);
        block.CaptureMouse();
        e.Handled = true;
    }

    private void TextVisual_MouseMove(TextElementViewModel element, TextBlock block, MouseEventArgs e)
    {
        if (!_isDraggingText || !ReferenceEquals(_selectedTextElement, element)) return;
        var pos = e.GetPosition(PageSurface);
        var dx = pos.X - _dragStartMouse.X;
        var dy = pos.Y - _dragStartMouse.Y;
        element.X = _dragStartElement.X + dx;
        element.Y = _dragStartElement.Y + dy;
        InkCanvas.SetLeft(block, element.X);
        InkCanvas.SetTop(block, element.Y);
    }

    private void TextVisual_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingText) return;
        _isDraggingText = false;
        (sender as TextBlock)?.ReleaseMouseCapture();
        ViewModel.IsDirty = true;
    }

    private void BeginTextEdit(LayerViewModel layer, TextElementViewModel element, TextBlock block)
    {
        var tb = new TextBox
        {
            Text = element.Text,
            MinWidth = 80,
            FontSize = element.FontSize,
            Foreground = new SolidColorBrush(element.Color),
            Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1)
        };
        Canvas.SetLeft(tb, element.X);
        Canvas.SetTop(tb, element.Y);
        OverlayCanvas.IsHitTestVisible = true;
        OverlayCanvas.Children.Add(tb);
        block.Visibility = Visibility.Collapsed;

        void Commit()
        {
            tb.KeyDown -= KeyHandler;
            tb.LostFocus -= LostFocusHandler;
            OverlayCanvas.Children.Remove(tb);
            OverlayCanvas.IsHitTestVisible = false;

            if (!string.IsNullOrWhiteSpace(tb.Text))
            {
                element.Text = tb.Text;
                block.Text = tb.Text;
                block.Visibility = Visibility.Visible;
                ViewModel.IsDirty = true;
            }
            else
            {
                layer.TextElements.Remove(element);
            }
        }

        void Cancel()
        {
            tb.KeyDown -= KeyHandler;
            tb.LostFocus -= LostFocusHandler;
            OverlayCanvas.Children.Remove(tb);
            OverlayCanvas.IsHitTestVisible = false;
            block.Visibility = Visibility.Visible;
        }

        void KeyHandler(object s, KeyEventArgs ke)
        {
            if (ke.Key == Key.Enter) { Commit(); ke.Handled = true; }
            else if (ke.Key == Key.Escape) { Cancel(); ke.Handled = true; }
        }

        void LostFocusHandler(object s, RoutedEventArgs re) => Commit();

        tb.KeyDown += KeyHandler;
        tb.LostFocus += LostFocusHandler;
        tb.Focus();
        tb.SelectAll();
    }

    private void DeleteSelectedTextElement()
    {
        if (_selectedTextElement is null) return;
        var element = _selectedTextElement;
        var layer = ViewModel.CurrentPage?.Layers.FirstOrDefault(l => l.TextElements.Contains(element));
        if (layer is null) return;

        var index = layer.TextElements.IndexOf(element);
        layer.TextElements.Remove(element);
        ViewModel.Undo.Push(new TextElementRemovedAction(layer.TextElements, element, index));
        ViewModel.IsDirty = true;
        _selectedTextElement = null;
    }

    #endregion

    #region Formas

    private static bool IsShapeTool(ToolType t) =>
        t is ToolType.Rectangle or ToolType.Ellipse or ToolType.Line or ToolType.Arrow;

    private void BeginShapeDrawing(ToolType tool, Point start)
    {
        _isDrawingShape = true;
        _drawingTool = tool;
        _shapeStart = start;

        Shape shape = tool switch
        {
            ToolType.Rectangle => new Rectangle(),
            ToolType.Ellipse => new Ellipse(),
            _ => new Line()
        };
        shape.Stroke = new SolidColorBrush(ViewModel.StrokeColor);
        shape.StrokeThickness = ViewModel.StrokeThickness;
        shape.IsHitTestVisible = false;

        OverlayCanvas.Children.Add(shape);
        _previewShape = shape;
        UpdateShapePreview(start);
    }

    private void UpdateShapePreview(Point current)
    {
        if (_previewShape is null) return;

        if (_previewShape is Line line)
        {
            line.X1 = _shapeStart.X;
            line.Y1 = _shapeStart.Y;
            line.X2 = current.X;
            line.Y2 = current.Y;
        }
        else
        {
            var x = Math.Min(_shapeStart.X, current.X);
            var y = Math.Min(_shapeStart.Y, current.Y);
            var w = Math.Abs(current.X - _shapeStart.X);
            var h = Math.Abs(current.Y - _shapeStart.Y);
            Canvas.SetLeft(_previewShape, x);
            Canvas.SetTop(_previewShape, y);
            _previewShape.Width = w;
            _previewShape.Height = h;
        }
    }

    private void FinalizeShapeDrawing(Point end)
    {
        if (_previewShape is not null)
        {
            OverlayCanvas.Children.Remove(_previewShape);
            _previewShape = null;
        }

        _isDrawingShape = false;

        var layer = ViewModel.CurrentPage?.ActiveLayer;
        if (layer is null || layer.IsLocked || !layer.IsVisible) return;

        if (Distance(_shapeStart, end) < 2 && _drawingTool != ToolType.Arrow) return;

        var strokes = BuildShapeStrokes(_drawingTool, _shapeStart, end, ViewModel.StrokeColor, ViewModel.StrokeThickness);
        if (strokes.Count > 0) layer.Strokes.Add(strokes);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

    private static StrokeCollection BuildShapeStrokes(ToolType tool, Point start, Point end, Color color, double thickness)
    {
        var strokes = new StrokeCollection();
        var attributes = new DrawingAttributes
        {
            Color = color,
            Width = thickness,
            Height = thickness,
            FitToCurve = true
        };

        switch (tool)
        {
            case ToolType.Rectangle:
            {
                var pts = new StylusPointCollection(new[]
                {
                    new Point(start.X, start.Y),
                    new Point(end.X, start.Y),
                    new Point(end.X, end.Y),
                    new Point(start.X, end.Y),
                    new Point(start.X, start.Y)
                });
                strokes.Add(new Stroke(pts, attributes.Clone()));
                break;
            }
            case ToolType.Ellipse:
            {
                var pts = new StylusPointCollection();
                var cx = (start.X + end.X) / 2;
                var cy = (start.Y + end.Y) / 2;
                var rx = Math.Abs(end.X - start.X) / 2;
                var ry = Math.Abs(end.Y - start.Y) / 2;
                const int segments = 64;
                for (var i = 0; i <= segments; i++)
                {
                    var angle = 2 * Math.PI * i / segments;
                    pts.Add(new StylusPoint(cx + rx * Math.Cos(angle), cy + ry * Math.Sin(angle)));
                }
                strokes.Add(new Stroke(pts, attributes.Clone()));
                break;
            }
            case ToolType.Line:
            {
                var pts = new StylusPointCollection(new[] { start, end });
                strokes.Add(new Stroke(pts, attributes.Clone()));
                break;
            }
            case ToolType.Arrow:
            {
                var shaft = new StylusPointCollection(new[] { start, end });
                strokes.Add(new Stroke(shaft, attributes.Clone()));

                var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
                var headLength = Math.Max(10, Distance(start, end) * 0.15);
                const double headAngle = Math.PI / 7;

                var left = new Point(
                    end.X - headLength * Math.Cos(angle - headAngle),
                    end.Y - headLength * Math.Sin(angle - headAngle));
                var right = new Point(
                    end.X - headLength * Math.Cos(angle + headAngle),
                    end.Y - headLength * Math.Sin(angle + headAngle));

                strokes.Add(new Stroke(new StylusPointCollection(new[] { left, end }), attributes.Clone()));
                strokes.Add(new Stroke(new StylusPointCollection(new[] { right, end }), attributes.Clone()));
                break;
            }
        }

        return strokes;
    }

    #endregion

    #region Lienzo: paneo, zoom y entrada de ratón

    private void ViewportBorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            StartPan(e.GetPosition(ViewportBorder));
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left) return;

        var tool = ViewModel.CurrentTool;

        if (tool == ToolType.Pan)
        {
            StartPan(e.GetPosition(ViewportBorder));
            e.Handled = true;
            return;
        }

        var layer = ViewModel.CurrentPage?.ActiveLayer;

        if (IsShapeTool(tool))
        {
            if (layer is null || layer.IsLocked || !layer.IsVisible) return;
            BeginShapeDrawing(tool, e.GetPosition(PageSurface));
            ViewportBorder.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (tool == ToolType.Text)
        {
            if (layer is null || layer.IsLocked || !layer.IsVisible) return;
            BeginTextEntry(e.GetPosition(PageSurface));
            e.Handled = true;
        }
    }

    private void StartPan(Point screenPoint)
    {
        _isPanning = true;
        _panLastScreenPoint = screenPoint;
        ViewportBorder.CaptureMouse();
        Mouse.OverrideCursor = Cursors.Hand;
    }

    private void ViewportBorder_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            var current = e.GetPosition(ViewportBorder);
            TranslateT.X += current.X - _panLastScreenPoint.X;
            TranslateT.Y += current.Y - _panLastScreenPoint.Y;
            _panLastScreenPoint = current;
            return;
        }

        if (_isDrawingShape)
        {
            UpdateShapePreview(e.GetPosition(PageSurface));
        }
    }

    private void ViewportBorder_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning && e.ChangedButton is MouseButton.Middle or MouseButton.Left)
        {
            _isPanning = false;
            ViewportBorder.ReleaseMouseCapture();
            Mouse.OverrideCursor = null;
            return;
        }

        if (_isDrawingShape && e.ChangedButton == MouseButton.Left)
        {
            var end = e.GetPosition(PageSurface);
            ViewportBorder.ReleaseMouseCapture();
            FinalizeShapeDrawing(end);
        }
    }

    private void ViewportBorder_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var pos = e.GetPosition(ViewportBorder);
        ApplyZoomAt(pos, e.Delta > 0 ? 1.1 : 1 / 1.1);
    }

    private void ApplyZoomAt(Point screenPoint, double factor)
    {
        var oldScale = ScaleT.ScaleX;
        var newScale = Math.Clamp(oldScale * factor, 0.05, 8.0);

        var boardX = (screenPoint.X - TranslateT.X) / oldScale;
        var boardY = (screenPoint.Y - TranslateT.Y) / oldScale;

        ScaleT.ScaleX = newScale;
        ScaleT.ScaleY = newScale;
        TranslateT.X = screenPoint.X - boardX * newScale;
        TranslateT.Y = screenPoint.Y - boardY * newScale;

        ViewModel.ZoomPercent = newScale * 100;
    }

    private void ApplyZoom(double factor)
    {
        var center = new Point(ViewportBorder.ActualWidth / 2, ViewportBorder.ActualHeight / 2);
        ApplyZoomAt(center, factor);
    }

    private void FitPageToView()
    {
        var page = ViewModel.CurrentPage;
        if (page is null || page.Width <= 0 || page.Height <= 0) return;
        if (ViewportBorder.ActualWidth <= 0 || ViewportBorder.ActualHeight <= 0) return;

        var scale = Math.Min(ViewportBorder.ActualWidth / page.Width, ViewportBorder.ActualHeight / page.Height) * 0.95;
        scale = Math.Clamp(scale, 0.02, 4.0);

        ScaleT.ScaleX = scale;
        ScaleT.ScaleY = scale;

        var offsetX = (ViewportBorder.ActualWidth - page.Width * scale) / 2;
        var offsetY = (ViewportBorder.ActualHeight - page.Height * scale) / 2;
        TranslateT.X = Math.Max(offsetX, 0);
        TranslateT.Y = Math.Max(offsetY, 0);

        ViewModel.ZoomPercent = scale * 100;
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ApplyZoom(1.2);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ApplyZoom(1 / 1.2);
    private void ZoomFit_Click(object sender, RoutedEventArgs e) => FitPageToView();

    #endregion

    #region Atajos de teclado

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (ctrl && shift && e.Key == Key.S) { SaveAs_Click(sender, e); e.Handled = true; }
        else if (ctrl && e.Key == Key.S) { _ = SaveInternal(ViewModel.FilePath); e.Handled = true; }
        else if (ctrl && e.Key == Key.O) { Open_Click(sender, e); e.Handled = true; }
        else if (ctrl && e.Key == Key.N) { AddPage_Click(sender, e); e.Handled = true; }
        else if (ctrl && e.Key == Key.Z) { ViewModel.Undo.Undo(); e.Handled = true; }
        else if (ctrl && e.Key == Key.Y) { ViewModel.Undo.Redo(); e.Handled = true; }
        else if (ctrl && e.Key is Key.OemPlus or Key.Add) { ApplyZoom(1.2); e.Handled = true; }
        else if (ctrl && e.Key is Key.OemMinus or Key.Subtract) { ApplyZoom(1 / 1.2); e.Handled = true; }
        else if (ctrl && e.Key == Key.D0) { FitPageToView(); e.Handled = true; }
        else if (e.Key == Key.Delete && _selectedTextElement is not null && ViewModel.CurrentTool == ToolType.Select)
        {
            DeleteSelectedTextElement();
            e.Handled = true;
        }
    }

    #endregion

    #region Archivo: nuevo, abrir, guardar, importar, exportar

    private bool ConfirmDiscardChanges()
    {
        if (!ViewModel.IsDirty) return true;
        var result = MessageBox.Show(this,
            "Hay cambios sin guardar. ¿Deseas continuar y descartarlos?",
            "Pizarra Pro", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.IsDirty) return;
        var result = MessageBox.Show(this,
            "Hay cambios sin guardar. ¿Deseas salir de todas formas?",
            "Pizarra Pro", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) e.Cancel = true;
    }

    private void NewDocument_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges()) return;
        ViewModel.ResetToNewDocument();
    }

    private void AddPage_Click(object sender, RoutedEventArgs e) => ViewModel.CreateBlankPage();

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges()) return;

        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Pizarra Pro (*.pzp)|*.pzp" };
        if (dlg.ShowDialog(this) != true) return;

        ViewModel.IsBusy = true;
        ViewModel.BusyMessage = "Abriendo documento...";
        try
        {
            var dto = await Task.Run(() => DocumentStorageService.Load(dlg.FileName));
            ViewModel.LoadFromDto(dto);
            ViewModel.FilePath = dlg.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo abrir el documento:\n{ex.Message}", "Pizarra Pro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveInternal(ViewModel.FilePath);

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Pizarra Pro (*.pzp)|*.pzp", FileName = "Documento.pzp" };
        if (dlg.ShowDialog(this) != true) return;
        await SaveInternal(dlg.FileName);
    }

    private async Task SaveInternal(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Pizarra Pro (*.pzp)|*.pzp", FileName = "Documento.pzp" };
            if (dlg.ShowDialog(this) != true) return;
            path = dlg.FileName;
        }

        ViewModel.IsBusy = true;
        ViewModel.BusyMessage = "Guardando...";
        await Dispatcher.Yield(DispatcherPriority.Background);
        try
        {
            // Los trazos son objetos de WPF ligados al hilo de interfaz: se serializan aquí mismo, sin Task.Run.
            DocumentStorageService.Save(path, ViewModel.Pages);
            ViewModel.FilePath = path;
            ViewModel.IsDirty = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo guardar el documento:\n{ex.Message}", "Pizarra Pro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private async void ImportPdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Documentos PDF (*.pdf)|*.pdf" };
        if (dlg.ShowDialog(this) != true) return;

        ViewModel.IsBusy = true;
        ViewModel.BusyMessage = "Importando PDF...";
        try
        {
            var pages = await Task.Run(() => PdfImportService.RenderPages(dlg.FileName));
            var pageNumber = 1;
            foreach (var bitmap in pages)
            {
                ViewModel.CreatePdfPage(bitmap, pageNumber++);
            }

            if (ViewModel.Pages.Count > 0) ViewModel.CurrentPageIndex = ViewModel.Pages.Count - 1;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo importar el PDF:\n{ex.Message}", "Pizarra Pro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private async void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        var page = ViewModel.CurrentPage;
        if (page is null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Imagen PNG (*.png)|*.png", FileName = "Pagina.png" };
        if (dlg.ShowDialog(this) != true) return;

        ViewModel.IsBusy = true;
        ViewModel.BusyMessage = "Exportando imagen...";
        await Dispatcher.Yield(DispatcherPriority.Background);
        try
        {
            ImageExportService.ExportPagePng(page, dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo exportar la imagen:\n{ex.Message}", "Pizarra Pro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Documento PDF (*.pdf)|*.pdf", FileName = "Documento.pdf" };
        if (dlg.ShowDialog(this) != true) return;

        ViewModel.IsBusy = true;
        ViewModel.BusyMessage = "Exportando PDF...";
        await Dispatcher.Yield(DispatcherPriority.Background);
        try
        {
            PdfExportService.ExportDocument(ViewModel.Pages, dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo exportar el PDF:\n{ex.Message}", "Pizarra Pro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this,
            "Pizarra Pro\n\n" +
            "Pizarra digital de escritorio para Windows: lienzo infinito, dibujo a mano y anotación de PDF.\n\n" +
            "Proyecto independiente inspirado en la idea de las pizarras digitales; no afiliado a Starnote.",
            "Acerca de Pizarra Pro", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion
}
