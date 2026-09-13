using System.Collections.ObjectModel;
using System.Windows.Media;

namespace PizarraPro.ViewModels;

public class PageViewModel : ViewModelBase
{
    public const double DefaultBoardWidth = 8000;
    public const double DefaultBoardHeight = 5000;

    private string _title;
    public string Title { get => _title; set => SetField(ref _title, value); }

    private double _width = DefaultBoardWidth;
    public double Width { get => _width; set => SetField(ref _width, value); }

    private double _height = DefaultBoardHeight;
    public double Height { get => _height; set => SetField(ref _height, value); }

    private ImageSource? _background;
    public ImageSource? Background { get => _background; set => SetField(ref _background, value); }

    private bool _isFromPdf;
    public bool IsFromPdf { get => _isFromPdf; set => SetField(ref _isFromPdf, value); }

    public string KindLabel => IsFromPdf ? "PDF" : "En blanco";

    public ObservableCollection<LayerViewModel> Layers { get; } = new();

    private LayerViewModel? _activeLayer;
    public LayerViewModel? ActiveLayer
    {
        get => _activeLayer;
        set
        {
            if (ReferenceEquals(_activeLayer, value)) return;
            if (_activeLayer is not null) _activeLayer.IsActive = false;
            _activeLayer = value;
            if (_activeLayer is not null) _activeLayer.IsActive = true;
            OnPropertyChanged();
        }
    }

    public PageViewModel(string title)
    {
        _title = title;
    }
}
