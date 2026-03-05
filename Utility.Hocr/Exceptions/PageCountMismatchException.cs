namespace Utility.Hocr.Exceptions;

/// <summary>
/// Thrown when the output PDF has a different page count than the input,
/// indicating data loss during processing.
/// </summary>
public class PageCountMismatchException : Exception
{
    public PageCountMismatchException(string msg, int pageCountStart, int pageCountEnd) : base(msg)
    {
        PageCountStart = pageCountStart;
        PageCountEnd = pageCountEnd;
    }

    public int PageCountStart { get; }
    public int PageCountEnd { get; }
}