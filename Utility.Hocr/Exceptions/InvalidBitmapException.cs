namespace Utility.Hocr.Exceptions;

/// <summary>
/// Thrown when an image cannot be converted to a valid bitmap.
/// </summary>
public class InvalidBitmapException : Exception
{
    public InvalidBitmapException(string msg, Exception inner) : base(msg, inner)
    {
    }
}