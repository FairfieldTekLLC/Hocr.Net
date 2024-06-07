namespace Utility.Hocr.Exceptions;

public class InvalidBitmapException : Exception
{
    public InvalidBitmapException(string msg, Exception inner) : base(msg, inner)
    {
    }
}