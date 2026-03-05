namespace Utility.Hocr.Pdf;

/// <summary>
/// Metadata properties to embed in a generated PDF document.
/// </summary>
public class PdfMeta
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string KeyWords { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}