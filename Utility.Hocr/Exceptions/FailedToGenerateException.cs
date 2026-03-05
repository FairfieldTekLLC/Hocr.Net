namespace Utility.Hocr.Exceptions;

/// <summary>
/// Thrown when searchable PDF generation fails.
/// </summary>
public class FailedToGenerateException : Exception
{
    public FailedToGenerateException(string msg, Exception innerException) : base(msg, innerException)
    {
    }
}