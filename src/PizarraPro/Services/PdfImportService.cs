using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;

namespace PizarraPro.Services;

/// <summary>Renderiza las páginas de un PDF como imágenes de mapa de bits usando PDFium (Docnet.Core).</summary>
public static class PdfImportService
{
    private const double TargetDpi = 150.0;

    public static List<BitmapSource> RenderPages(string pdfPath)
    {
        var bytes = File.ReadAllBytes(pdfPath);
        return RenderPages(bytes);
    }

    public static List<BitmapSource> RenderPages(byte[] pdfBytes)
    {
        var result = new List<BitmapSource>();
        var library = DocLib.Instance;

        using var docReader = library.GetDocReader(pdfBytes, new PageDimensions(TargetDpi / 72.0));
        var pageCount = docReader.GetPageCount();

        for (var i = 0; i < pageCount; i++)
        {
            using var pageReader = docReader.GetPageReader(i);
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();
            var raw = pageReader.GetImage();

            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, raw, width * 4);
            bitmap.Freeze();
            result.Add(bitmap);
        }

        return result;
    }
}
