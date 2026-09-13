using System.Windows.Media;

namespace PizarraPro.ViewModels;

public class TextElementViewModel : ViewModelBase
{
    private string _text = string.Empty;
    public string Text { get => _text; set => SetField(ref _text, value); }

    private double _x;
    public double X { get => _x; set => SetField(ref _x, value); }

    private double _y;
    public double Y { get => _y; set => SetField(ref _y, value); }

    private double _fontSize = 24;
    public double FontSize { get => _fontSize; set => SetField(ref _fontSize, value); }

    private Color _color = Colors.Black;
    public Color Color { get => _color; set => SetField(ref _color, value); }

    private string _fontFamily = "Segoe UI";
    public string FontFamily { get => _fontFamily; set => SetField(ref _fontFamily, value); }
}
