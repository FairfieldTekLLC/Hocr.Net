namespace Utility.Hocr.Exceptions;

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