namespace Utility.Hocr.Enums;

/// <summary>
/// GhostScript distiller quality presets, controlling the balance between
/// output file size and image quality.
/// </summary>
public enum dPdfSettings
{
    screen,
    ebook,
    printer,
    prepress,
    Default
}