using System.Collections.Generic;

namespace PizarraPro.Models;

/// <summary>Formato serializable del documento .pzp (JSON).</summary>
public class DocumentDto
{
    public string FormatVersion { get; set; } = "1.0";

    public List<PageDto> Pages { get; set; } = new();
}

public class PageDto
{
    public string Title { get; set; } = "Página";

    public double Width { get; set; }

    public double Height { get; set; }

    public bool IsFromPdf { get; set; }

    public byte[]? BackgroundPng { get; set; }

    public List<LayerDto> Layers { get; set; } = new();
}

public class LayerDto
{
    public string Name { get; set; } = "Capa";

    public bool IsVisible { get; set; } = true;

    public bool IsLocked { get; set; }

    public double Opacity { get; set; } = 1.0;

    /// <summary>Trazos serializados en formato ISF (Ink Serialized Format).</summary>
    public byte[]? StrokesIsf { get; set; }

    public List<TextElementDto> TextElements { get; set; } = new();
}

public class TextElementDto
{
    public string Text { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public double FontSize { get; set; } = 20;

    public string Color { get; set; } = "#FF000000";

    public string FontFamily { get; set; } = "Segoe UI";
}
