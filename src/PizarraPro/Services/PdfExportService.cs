using System;
using System.Collections.Generic;
using System.IO;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PizarraPro.ViewModels;

namespace PizarraPro.Services;

/// <summary>Aplana cada página (fondo + capas) a una imagen y compone un PDF final con PdfSharpCore.</summary>
public static class PdfExportService
{
    public static void ExportDocument(IReadOnlyList<PageViewModel> pages, string path, double dpi = 150)
    {
        using var document = new PdfDocument();

        foreach (var page in pages)
        {
            var bitmap = ImageExportService.RenderPageToBitmap(page, dpi);
            var tempFile = Path.Combine(Path.GetTempPath(), $"pizarrapro_{Guid.NewGuid():N}.png");

            try
            {
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var fs = File.Create(tempFile))
                {
                    encoder.Save(fs);
                }

                var widthPoints = page.Width / 96.0 * 72.0;
                var heightPoints = page.Height / 96.0 * 72.0;

                var pdfPage = document.AddPage();
                pdfPage.Width = widthPoints;
                pdfPage.Height = heightPoints;

                using var gfx = XGraphics.FromPdfPage(pdfPage);
                using var image = XImage.FromFile(tempFile);
                gfx.DrawImage(image, 0, 0, widthPoints, heightPoints);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        document.Save(path);
    }
}
