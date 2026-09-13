using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PizarraPro.ViewModels;

namespace PizarraPro.Services;

/// <summary>Renderiza (aplana) una página -fondo + capas visibles- a un mapa de bits fuera de pantalla.</summary>
public static class ImageExportService
{
    public static RenderTargetBitmap RenderPageToBitmap(PageViewModel page, double dpi = 150)
    {
        var scale = dpi / 96.0;
        var pixelWidth = Math.Max(1, (int)Math.Round(page.Width * scale));
        var pixelHeight = Math.Max(1, (int)Math.Round(page.Height * scale));

        var root = new Grid
        {
            Width = page.Width,
            Height = page.Height,
            Background = Brushes.White
        };

        if (page.Background is not null)
        {
            root.Children.Add(new Image
            {
                Source = page.Background,
                Width = page.Width,
                Height = page.Height,
                Stretch = Stretch.Fill
            });
        }

        foreach (var layer in page.Layers)
        {
            if (!layer.IsVisible) continue;

            var ink = new InkCanvas
            {
                Width = page.Width,
                Height = page.Height,
                Background = Brushes.Transparent,
                Opacity = layer.Opacity,
                IsHitTestVisible = false,
                EditingMode = InkCanvasEditingMode.None,
                Strokes = layer.Strokes
            };

            foreach (var t in layer.TextElements)
            {
                var block = new TextBlock
                {
                    Text = t.Text,
                    FontSize = t.FontSize,
                    FontFamily = new FontFamily(t.FontFamily),
                    Foreground = new SolidColorBrush(t.Color)
                };
                InkCanvas.SetLeft(block, t.X);
                InkCanvas.SetTop(block, t.Y);
                ink.Children.Add(block);
            }

            root.Children.Add(ink);
        }

        var size = new Size(page.Width, page.Height);
        root.Measure(size);
        root.Arrange(new Rect(size));
        root.UpdateLayout();

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(root);
        return rtb;
    }

    public static void ExportPagePng(PageViewModel page, string path, double dpi = 150)
    {
        var rtb = RenderPageToBitmap(page, dpi);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
