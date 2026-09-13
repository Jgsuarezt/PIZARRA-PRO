using System.Collections.ObjectModel;
using System.Windows.Ink;

namespace PizarraPro.ViewModels;

public class LayerViewModel : ViewModelBase
{
    private string _name;
    public string Name { get => _name; set => SetField(ref _name, value); }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set { if (SetField(ref _isVisible, value)) OnPropertyChanged(nameof(CanEdit)); }
    }

    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        set { if (SetField(ref _isLocked, value)) OnPropertyChanged(nameof(CanEdit)); }
    }

    private double _opacity = 1.0;
    public double Opacity { get => _opacity; set => SetField(ref _opacity, value); }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set { if (SetField(ref _isActive, value)) OnPropertyChanged(nameof(CanEdit)); }
    }

    /// <summary>Verdadero cuando esta capa puede recibir trazos/edición del usuario.</summary>
    public bool CanEdit => IsActive && IsVisible && !IsLocked;

    /// <summary>Colección de trazos de tinta de esta capa (compartida con el InkCanvas visual).</summary>
    public StrokeCollection Strokes { get; } = new();

    public ObservableCollection<TextElementViewModel> TextElements { get; } = new();

    public LayerViewModel(string name)
    {
        _name = name;
    }
}
