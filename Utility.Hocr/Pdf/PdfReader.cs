using Utility.Hocr.ImageProcessors;

namespace Utility.Hocr.Pdf;

internal class PdfReader : IDisposable
{
    private readonly int _dpi;

    public PdfReader(string sourcePdf, int dpi, string ghostscriptPath)
    {
        GhostScriptPath = ghostscriptPath;
        SourcePdf = sourcePdf;
        TextReader = new iTextSharp.text.pdf.PdfReader(sourcePdf);
        _dpi = dpi;
    }

    private string GhostScriptPath { get; }

    public iTextSharp.text.pdf.PdfReader TextReader { get; private set; }
    public int PageCount => TextReader.NumberOfPages;
    public string SourcePdf { get; }

    public void Dispose()
    {
        try
        {
            TextReader?.Close();
            TextReader?.Dispose();
        }
        catch (Exception)
        {
            //Just continue on.
        }

        TextReader = null;
    }

    public string GetPageImage(int pageNumber, string sessionName, PdfCompressor pdfCompressor)
    {
        return GetPageImageWithGhostScript(pageNumber, sessionName);
    }

    private string GetPageImageWithGhostScript(int pageNumber, string sessionName)
    {
        GhostScript g = new(GhostScriptPath, _dpi);
        string imgFile = g.ConvertPdfToBitmap(SourcePdf, pageNumber, pageNumber, sessionName);
        return imgFile;
    }
}