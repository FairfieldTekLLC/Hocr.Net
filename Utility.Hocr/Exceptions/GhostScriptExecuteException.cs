namespace Utility.Hocr.Exceptions;

/// <summary>
/// Thrown when a GhostScript command-line operation fails.
/// </summary>
public class GhostScriptExecuteException : Exception
{
    public GhostScriptExecuteException(string msg, Exception innerException) : base(msg, innerException)
    {
    }
}