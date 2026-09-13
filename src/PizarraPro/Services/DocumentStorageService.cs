using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PizarraPro.Models;
using PizarraPro.ViewModels;

namespace PizarraPro.Services;

/// <summary>Guarda y carga documentos de Pizarra Pro (.pzp) como JSON autocontenido.</summary>
public static class DocumentStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public static void Save(string path, IReadOnlyList<PageViewModel> pages)
    {
        var dto = new DocumentDto();

        foreach (var page in pages)
        {
            var pageDto = new PageDto
            {
                Title = page.Title,
                Width = page.Width,
                Height = page.Height,
                IsFromPdf = page.IsFromPdf,
                BackgroundPng = EncodeBackground(page.Background)
            };

            foreach (var layer in page.Layers)
            {
                var layerDto = new LayerDto
                {
                    Name = layer.Name,
                    IsVisible = layer.IsVisible,
                    IsLocked = layer.IsLocked,
                    Opacity = layer.Opacity,
                    StrokesIsf = SerializeStrokes(layer.Strokes)
                };

                foreach (var t in layer.TextElements)
                {
                    layerDto.TextElements.Add(new TextElementDto
                    {
                        Text = t.Text,
                        X = t.X,
                        Y = t.Y,
                        FontSize = t.FontSize,
                        Color = t.Color.ToString(),
                        FontFamily = t.FontFamily
                    });
                }

                pageDto.Layers.Add(layerDto);
            }

            dto.Pages.Add(pageDto);
        }

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static DocumentDto Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DocumentDto>(json) ?? new DocumentDto();
    }

    public static byte[]? SerializeStrokes(StrokeCollection strokes)
    {
        if (strokes.Count == 0) return null;
        using var ms = new MemoryStream();
        strokes.Save(ms);
        return ms.ToArray();
    }

    public static StrokeCollection DeserializeStrokes(byte[]? data)
    {
        if (data is null || data.Length == 0) return new StrokeCollection();
        using var ms = new MemoryStream(data);
        return new StrokeCollection(ms);
    }

    private static byte[]? EncodeBackground(ImageSource? source)
    {
        if (source is not BitmapSource bmp) return null;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    public static BitmapImage? DecodeBackground(byte[]? data)
    {
        if (data is null || data.Length == 0) return null;
        using var ms = new MemoryStream(data);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
