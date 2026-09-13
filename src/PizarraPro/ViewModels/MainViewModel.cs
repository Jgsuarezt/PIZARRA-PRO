using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PizarraPro.Models;
using PizarraPro.Services;

namespace PizarraPro.ViewModels;

public class MainViewModel : ViewModelBase
{
    public ObservableCollection<PageViewModel> Pages { get; } = new();

    private int _currentPageIndex = -1;
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            var clamped = Pages.Count == 0 ? -1 : Math.Clamp(value, 0, Pages.Count - 1);
            if (SetField(ref _currentPageIndex, clamped))
            {
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(PageIndicator));
            }
        }
    }

    public PageViewModel? CurrentPage =>
        CurrentPageIndex >= 0 && CurrentPageIndex < Pages.Count ? Pages[CurrentPageIndex] : null;

    public string PageIndicator =>
        Pages.Count == 0 ? "Sin páginas" : $"Página {CurrentPageIndex + 1} de {Pages.Count}";

    private ToolType _currentTool = ToolType.Pen;
    public ToolType CurrentTool { get => _currentTool; set => SetField(ref _currentTool, value); }

    private Color _strokeColor = Colors.Black;
    public Color StrokeColor { get => _strokeColor; set => SetField(ref _strokeColor, value); }

    private double _strokeThickness = 3;
    public double StrokeThickness { get => _strokeThickness; set => SetField(ref _strokeThickness, value); }

    private double _textFontSize = 24;
    public double TextFontSize { get => _textFontSize; set => SetField(ref _textFontSize, value); }

    private double _zoomPercent = 100;
    public double ZoomPercent { get => _zoomPercent; set => SetField(ref _zoomPercent, value); }

    private string? _filePath;
    public string? FilePath
    {
        get => _filePath;
        set { if (SetField(ref _filePath, value)) OnPropertyChanged(nameof(WindowTitle)); }
    }

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        set { if (SetField(ref _isDirty, value)) OnPropertyChanged(nameof(WindowTitle)); }
    }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }

    private string _busyMessage = "Procesando...";
    public string BusyMessage { get => _busyMessage; set => SetField(ref _busyMessage, value); }

    public string WindowTitle
    {
        get
        {
            var name = string.IsNullOrEmpty(FilePath) ? "Sin título" : Path.GetFileName(FilePath);
            return $"Pizarra Pro - {name}{(IsDirty ? " *" : string.Empty)}";
        }
    }

    public UndoRedoManager Undo { get; } = new();

    public RelayCommand NewBoardCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand PrevPageCommand { get; }
    public RelayCommand DuplicatePageCommand { get; }
    public RelayCommand DeletePageCommand { get; }
    public RelayCommand AddLayerCommand { get; }
    public RelayCommand RemoveLayerCommand { get; }
    public RelayCommand MoveLayerUpCommand { get; }
    public RelayCommand MoveLayerDownCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }

    public MainViewModel()
    {
        NewBoardCommand = new RelayCommand(_ => CreateBlankPage());
        NextPageCommand = new RelayCommand(_ => CurrentPageIndex++, _ => CurrentPageIndex < Pages.Count - 1);
        PrevPageCommand = new RelayCommand(_ => CurrentPageIndex--, _ => CurrentPageIndex > 0);
        DuplicatePageCommand = new RelayCommand(_ => DuplicateCurrentPage(), _ => CurrentPage is not null);
        DeletePageCommand = new RelayCommand(_ => DeleteCurrentPage(), _ => Pages.Count > 1);
        AddLayerCommand = new RelayCommand(_ =>
        {
            if (CurrentPage is not null) CreateLayer(CurrentPage, null);
        }, _ => CurrentPage is not null);
        RemoveLayerCommand = new RelayCommand(p => RemoveLayer(p as LayerViewModel),
            p => CurrentPage is not null && CurrentPage.Layers.Count > 1 && p is LayerViewModel);
        MoveLayerUpCommand = new RelayCommand(p => MoveLayer(p as LayerViewModel, -1));
        MoveLayerDownCommand = new RelayCommand(p => MoveLayer(p as LayerViewModel, 1));
        UndoCommand = new RelayCommand(_ => Undo.Undo(), _ => Undo.CanUndo);
        RedoCommand = new RelayCommand(_ => Undo.Redo(), _ => Undo.CanRedo);

        Undo.StateChanged += (_, _) =>
        {
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        };

        CreateBlankPage();
        IsDirty = false;
    }

    public PageViewModel CreateBlankPage()
    {
        var page = new PageViewModel($"Página {Pages.Count + 1}");
        Pages.Add(page);
        CreateLayer(page, "Capa 1");
        CurrentPageIndex = Pages.Count - 1;
        IsDirty = true;
        return page;
    }

    public PageViewModel CreatePdfPage(BitmapSource background, int pageNumber)
    {
        var page = new PageViewModel($"PDF - Página {pageNumber}")
        {
            Width = background.PixelWidth,
            Height = background.PixelHeight,
            Background = background,
            IsFromPdf = true
        };
        Pages.Add(page);
        CreateLayer(page, "Anotaciones");
        IsDirty = true;
        return page;
    }

    public LayerViewModel CreateLayer(PageViewModel page, string? name)
    {
        var layer = new LayerViewModel(name ?? $"Capa {page.Layers.Count + 1}");
        layer.Strokes.StrokesChanged += (_, e) =>
        {
            if (Undo.IsApplying) return;
            Undo.Push(new StrokesChangedAction(layer.Strokes, e.Added, e.Removed));
            IsDirty = true;
        };
        page.Layers.Add(layer);
        page.ActiveLayer = layer;
        return layer;
    }

    private void RemoveLayer(LayerViewModel? layer)
    {
        if (layer is null || CurrentPage is null) return;
        if (CurrentPage.Layers.Count <= 1) return;

        var index = CurrentPage.Layers.IndexOf(layer);
        CurrentPage.Layers.Remove(layer);

        if (ReferenceEquals(CurrentPage.ActiveLayer, layer))
        {
            var newIndex = Math.Min(index, CurrentPage.Layers.Count - 1);
            CurrentPage.ActiveLayer = CurrentPage.Layers.Count > 0 ? CurrentPage.Layers[newIndex] : null;
        }

        IsDirty = true;
    }

    private void MoveLayer(LayerViewModel? layer, int direction)
    {
        if (layer is null || CurrentPage is null) return;
        var layers = CurrentPage.Layers;
        var index = layers.IndexOf(layer);
        var newIndex = index + direction;
        if (index < 0 || newIndex < 0 || newIndex >= layers.Count) return;
        layers.Move(index, newIndex);
        IsDirty = true;
    }

    private void DuplicateCurrentPage()
    {
        var source = CurrentPage;
        if (source is null) return;

        var copy = new PageViewModel(source.Title + " (copia)")
        {
            Width = source.Width,
            Height = source.Height,
            Background = source.Background,
            IsFromPdf = source.IsFromPdf
        };
        Pages.Insert(CurrentPageIndex + 1, copy);

        foreach (var srcLayer in source.Layers)
        {
            var layer = CreateLayer(copy, srcLayer.Name);
            layer.IsVisible = srcLayer.IsVisible;
            layer.Opacity = srcLayer.Opacity;

            var isf = DocumentStorageService.SerializeStrokes(srcLayer.Strokes);
            var clonedStrokes = DocumentStorageService.DeserializeStrokes(isf);
            if (clonedStrokes.Count > 0) layer.Strokes.Add(clonedStrokes);

            foreach (var t in srcLayer.TextElements)
            {
                layer.TextElements.Add(new TextElementViewModel
                {
                    Text = t.Text,
                    X = t.X,
                    Y = t.Y,
                    FontSize = t.FontSize,
                    Color = t.Color,
                    FontFamily = t.FontFamily
                });
            }
        }

        CurrentPageIndex++;
        IsDirty = true;
    }

    private void DeleteCurrentPage()
    {
        if (Pages.Count <= 1 || CurrentPage is null) return;
        var index = CurrentPageIndex;
        Pages.RemoveAt(index);
        CurrentPageIndex = Math.Min(index, Pages.Count - 1);
        IsDirty = true;
    }

    public void LoadFromDto(DocumentDto dto)
    {
        Pages.Clear();
        Undo.Clear();

        foreach (var pageDto in dto.Pages)
        {
            var page = new PageViewModel(pageDto.Title)
            {
                Width = pageDto.Width > 0 ? pageDto.Width : PageViewModel.DefaultBoardWidth,
                Height = pageDto.Height > 0 ? pageDto.Height : PageViewModel.DefaultBoardHeight,
                IsFromPdf = pageDto.IsFromPdf,
                Background = DocumentStorageService.DecodeBackground(pageDto.BackgroundPng)
            };
            Pages.Add(page);

            foreach (var layerDto in pageDto.Layers)
            {
                var layer = CreateLayer(page, layerDto.Name);
                layer.IsVisible = layerDto.IsVisible;
                layer.IsLocked = layerDto.IsLocked;
                layer.Opacity = layerDto.Opacity;

                var strokes = DocumentStorageService.DeserializeStrokes(layerDto.StrokesIsf);
                if (strokes.Count > 0) layer.Strokes.Add(strokes);

                foreach (var t in layerDto.TextElements)
                {
                    Color color;
                    try
                    {
                        color = (Color)(ColorConverter.ConvertFromString(t.Color) ?? Colors.Black);
                    }
                    catch
                    {
                        color = Colors.Black;
                    }

                    layer.TextElements.Add(new TextElementViewModel
                    {
                        Text = t.Text,
                        X = t.X,
                        Y = t.Y,
                        FontSize = t.FontSize,
                        Color = color,
                        FontFamily = t.FontFamily
                    });
                }
            }
        }

        if (Pages.Count == 0) CreateBlankPage();
        else CurrentPageIndex = 0;

        IsDirty = false;
    }

    public void ResetToNewDocument()
    {
        Pages.Clear();
        Undo.Clear();
        FilePath = null;
        CreateBlankPage();
        IsDirty = false;
    }
}
