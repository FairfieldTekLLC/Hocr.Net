using System.Diagnostics;
using Utility.Hocr.Enums;
using Utility.Hocr.Exceptions;

namespace Utility.Hocr.ImageProcessors;

/// <summary>
/// Wraps GhostScript command-line operations for PDF compression and
/// PDF-to-bitmap conversion.
/// </summary>
internal class GhostScript
{
    private readonly int _dpi;
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance with the path to the GhostScript executable and target DPI.
    /// </summary>
    /// <param name="path">Full path to the GhostScript executable (e.g., gswin64c.exe).</param>
    /// <param name="dpi">The resolution in dots per inch for image operations.</param>
    public GhostScript(string path, int dpi)
    {
        _path = path;
        _dpi = dpi;
    }

    /// <summary>
    /// Compresses a PDF file using GhostScript with the specified compatibility level,
    /// distiller settings, and additional options.
    /// </summary>
    /// <param name="inputPdf">Path to the source PDF file.</param>
    /// <param name="sessionName">The temp session name for output file creation.</param>
    /// <param name="level">The PDF compatibility level for the output.</param>
    /// <param name="dPdfSettings">The GhostScript distiller quality preset.</param>
    /// <param name="options">Additional GhostScript command-line options.</param>
    /// <returns>The path to the compressed output PDF file.</returns>
    /// <exception cref="GhostScriptExecuteException">GhostScript execution failed.</exception>
    public string CompressPdf(string inputPdf, string sessionName,
        PdfCompatibilityLevel level,
        dPdfSettings dPdfSettings = dPdfSettings.screen,
        string options = "")
    {
        try
        {
            string clevel;
            switch (level)
            {
                case PdfCompatibilityLevel.Acrobat_4_1_3:
                    clevel = "1.3";
                    break;
                case PdfCompatibilityLevel.Acrobat_5_1_4:
                    clevel = "1.4";
                    break;
                case PdfCompatibilityLevel.Acrobat_6_1_5:
                    clevel = "1.5";
                    break;
                case PdfCompatibilityLevel.Acrobat_7_1_6:
                    clevel = "1.6";
                    break;
                case PdfCompatibilityLevel.Acrobat_7_1_7:
                    clevel = "1.7";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }

            string outPutFileName = TempData.Instance.CreateTempFile(sessionName, ".pdf");
            string command =
                $@"-q -dNOPAUSE -dBATCH -dSAFER -sDEVICE=pdfwrite -dCompatibilityLevel={clevel} -dPDFSETTINGS=/{dPdfSettings} -dDetectDuplicateImages=true -dCompressFonts=true {options} -sOutputFile={'"'}{outPutFileName}{'"'} {'"'}{inputPdf}{'"'} -c quit";
            RunCommand(command);
            return outPutFileName;
        }
        catch (Exception e)
        {
            throw new GhostScriptExecuteException("Hocr.ImageProcessors.GhostScript - CompressPdf", e);
        }
    }

    /// <summary>
    /// Converts a range of PDF pages to a BMP bitmap image using GhostScript.
    /// </summary>
    /// <param name="pdf">Path to the source PDF file.</param>
    /// <param name="startPageNum">The first page number to convert (1-based).</param>
    /// <param name="endPageNum">The last page number to convert (1-based).</param>
    /// <param name="sessionName">The temp session name for output file creation.</param>
    /// <returns>The full path to the generated bitmap file.</returns>
    /// <exception cref="GhostScriptExecuteException">GhostScript execution failed.</exception>
    public string ConvertPdfToBitmap(string pdf, int startPageNum, int endPageNum, string sessionName)
    {
        try
        {
            string outPut = GetOutPutFileName(sessionName, ".bmp");
            pdf = "\"" + pdf + "\"";
            string command = string.Concat(
                $"-dNOPAUSE -q -r{_dpi} -sDEVICE=bmp16m -dBATCH -dGraphicsAlphaBits=4 -dTextAlphaBits=4 -dFirstPage=",
                startPageNum.ToString(), " -dLastPage=", endPageNum.ToString(),
                " -sOutputFile=" + outPut + " " + pdf + " -c quit");
            RunCommand(command);
            return new FileInfo(outPut.Replace('"', ' ').Trim()).FullName;
        }
        catch (Exception e)
        {
            throw new GhostScriptExecuteException("Hocr.ImageProcessors.GhostScript - ConvertPdfToBitmap", e);
        }
    }

    /// <summary>
    /// Creates a quoted temporary file path for use in GhostScript command-line arguments.
    /// </summary>
    private static string GetOutPutFileName(string sessionName, string extWithDot)
    {
        return "\"" + TempData.Instance.CreateTempFile(sessionName, extWithDot) + "\"";
    }

    /// <summary>
    /// Executes a GhostScript command-line process and waits for it to complete.
    /// Stdout and stderr are drained asynchronously to prevent deadlocks.
    /// </summary>
    /// <param name="command">The GhostScript command-line arguments.</param>
    private void RunCommand(string command)
    {
        ProcessStartInfo startexe = new(_path, command)
        {
            WorkingDirectory = Directory.GetCurrentDirectory(),
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        using Process proc = Process.Start(startexe);
        if (proc != null)
        {
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();
        }

        Debug.WriteLine("GhostScript exited.");
    }
}