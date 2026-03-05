using Utility.Hocr.ImageProcessors;

namespace Utility.Hocr.Pdf;

/// <summary>
/// Reads pages from a source PDF by converting each page to a bitmap image
/// using GhostScript. Wraps an iTextSharp PdfReader for page count access.
/// </summary>
internal class PdfReader : IDisposable
{
    private readonly int _dpi;

    /// <summary>
    /// Opens the specified PDF file for reading.
    /// </summary>
    /// <param name="sourcePdf">Path to the source PDF file.</param>
    /// <param name="dpi">The resolution in dots per inch for page image rendering.</param>
    /// <param name="ghostscriptPath">Full path to the GhostScript executable.</param>
    public PdfReader(string sourcePdf, int dpi, string ghostscriptPath)
    {
        GhostScriptPath = ghostscriptPath;
        SourcePdf = sourcePdf;
        TextReader = new iTextSharp.text.pdf.PdfReader(sourcePdf);
        _dpi = dpi;
    }

    private string GhostScriptPath { get; }

    public iTextSharp.text.pdf.PdfReader TextReader { get; private set; }

    /// <summary>Gets the total number of pages in the source PDF.</summary>
    public int PageCount => TextReader.NumberOfPages;

    public string SourcePdf { get; }

    /// <summary>
    /// Releases the underlying iTextSharp PdfReader resources.
    /// </summary>
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

    /// <summary>
    /// Renders a single PDF page as a bitmap image file using GhostScript.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number to render.</param>
    /// <param name="sessionName">The temp session name for output file creation.</param>
    /// <param name="pdfCompressor">The parent compressor instance (reserved for future use).</param>
    /// <returns>The full path to the rendered bitmap file.</returns>
    public string GetPageImage(int pageNumber, string sessionName, PdfCompressor pdfCompressor)
    {
        return GetPageImageWithGhostScript(pageNumber, sessionName);
    }

    /// <summary>
    /// Converts a PDF page to a BMP image using GhostScript.
    /// </summary>
    private string GetPageImageWithGhostScript(int pageNumber, string sessionName)
    {
        GhostScript g = new(GhostScriptPath, _dpi);
        string imgFile = g.ConvertPdfToBitmap(SourcePdf, pageNumber, pageNumber, sessionName);
        return imgFile;
    }
}