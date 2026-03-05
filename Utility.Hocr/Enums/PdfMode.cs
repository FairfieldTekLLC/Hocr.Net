namespace Utility.Hocr.Enums;

/// <summary>
/// Specifies how pages are rendered when building the output PDF.
/// </summary>
public enum PdfMode
{
    /// <summary>Render the page image and overlay invisible OCR text.</summary>
    Ocr,
    /// <summary>Draw colored bounding boxes around detected text regions for debugging.</summary>
    DrawBlocks,
    /// <summary>Emit only the OCR text with no background image.</summary>
    TextOnly,
    /// <summary>Embed only the page image with no text layer.</summary>
    ImageOnly,
    /// <summary>Combine DrawBlocks visualization with direct text overlay.</summary>
    Debug
}