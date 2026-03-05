using System.Collections.Concurrent;
using System.Drawing;
using iTextSharp.text.pdf;
using Utility.Hocr.Enums;
using Utility.Hocr.Exceptions;
using Utility.Hocr.ImageProcessors;
using Rectangle = iTextSharp.text.Rectangle;

namespace Utility.Hocr.Pdf;
#pragma warning disable CA1416

/// <summary>Raised when an exception occurs during PDF compression or OCR processing.</summary>
public delegate void CompressorExceptionOccurred(PdfCompressor c, Exception x);

/// <summary>Raised to report progress messages during PDF compression and OCR.</summary>
public delegate void CompressorEvent(string msg);

/// <summary>Raised to allow callers to pre-process a page image before OCR. Return the path to the processed image.</summary>
public delegate string PreProcessImage(string bitmapPath);

/// <summary>
/// Converts PDF files into searchable PDFs by performing OCR on each page
/// and embedding the recognized text as an invisible overlay. Supports
/// optional GhostScript-based compression of the final output.
/// </summary>
public class PdfCompressor:IDisposable
{
    private System.Timers.Timer CleanUpTimer;

    /// <summary>
    /// Initializes a new PDF compressor with the specified GhostScript path and settings.
    /// Starts a background timer that periodically cleans up completed sessions.
    /// </summary>
    /// <param name="ghostScriptPath">Full path to the GhostScript executable.</param>
    /// <param name="settings">Compression and OCR settings. Uses defaults if <c>null</c>.</param>
    public PdfCompressor(string ghostScriptPath, PdfCompressorSettings settings = null)
    {
        PdfSettings = settings ?? new PdfCompressorSettings();
        GhostScriptPath = ghostScriptPath;
        CleanUpTimer = new System.Timers.Timer();
        CleanUpTimer.Interval = 5000;
        CleanUpTimer.Elapsed += CleanUpTimer_Elapsed;
        CleanUpTimer.Start();
    }

    private void CleanUpTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        KeyValuePair<string, string>[] ar = SessionsToClean.ToArray();

        foreach (KeyValuePair<string, string> s in ar)
        {
            try
            {
                TempData.Instance.DestroySession(s.Value);
                SessionsToClean.Remove(s.Value, out string _);
            }
            catch (Exception exception)
            {
                //
            }
        }
    }

    private string GhostScriptPath { get; }

    ConcurrentDictionary<string, string> SessionsToClean { get; } = new ConcurrentDictionary<string, string>();



    public PdfCompressorSettings PdfSettings { get; }

    /// <summary>
    /// Reads each page of the input PDF as an image, performs OCR, and writes
    /// the image with invisible text overlay to the output PDF.
    /// </summary>
    private string CompressAndOcr(string sessionName, string inputFileName, string outputFileName, PdfMeta meta, bool firstPageOnly)
    {
        string pageBody = "";
        OnCompressorEvent?.Invoke(sessionName + " Creating PDF Reader");
        using (PdfReader reader = new(inputFileName, PdfSettings.Dpi, GhostScriptPath))
        {
            OnCompressorEvent?.Invoke(sessionName + " Creating PDF Writer");
            using (PdfCreator writer = new(PdfSettings, outputFileName, meta, PdfSettings.Dpi) { PdfSettings = { WriteTextMode = PdfSettings.WriteTextMode } })
            {
                try
                {
                    for (int i = 1; i <= reader.PageCount; i++)
                        try
                        {
                            OnCompressorEvent?.Invoke(sessionName + " Processing page " + i + " of " + reader.PageCount);
                            string ImageFileName = reader.GetPageImage(i, sessionName, this);
                            if (OnPreProcessImage != null)
                                ImageFileName = OnPreProcessImage(ImageFileName);
                            if (ImageFileName == null)
                                throw new Exception("Image Is Null!!");
                            Rectangle pageSize;
                            using (Bitmap chk = new(ImageFileName))
                            {
                                pageSize = new(0, 0, chk.Width, chk.Height);
                            }
                            pageBody += writer.AddPage(ImageFileName, PdfMode.Ocr, sessionName, pageSize);

                            if (firstPageOnly)
                                return (pageBody);
                        }
                        catch (Exception)
                        {
                            OnCompressorEvent?.Invoke(sessionName + $" Error reading page {i}  in " +
                                                      Path.GetFileName(inputFileName) + ". Skipping page");
                        }

                    writer.SaveAndClose();
                    return pageBody;
                }
                catch (Exception x)
                {
                    OnCompressorEvent?.Invoke(sessionName + " Image not supported in " +
                                              Path.GetFileName(inputFileName) +
                                              ". Skipping");
                    OnExceptionOccurred?.Invoke(this, x);
                    writer?.SaveAndClose();
                    return pageBody;
                }
            }
        }
    }


    /// <summary>
    /// Creates a searchable PDF from raw PDF file bytes by rendering each page as an image,
    /// performing OCR with Tesseract, and embedding the recognized text as an invisible overlay.
    /// Optionally compresses the final output using GhostScript.
    /// </summary>
    /// <param name="fileData">The raw bytes of the source PDF file.</param>
    /// <param name="metaData">Metadata (title, author, etc.) to embed in the output PDF.</param>
    /// <param name="firstPageOnly">If <c>true</c>, only the first page is processed.</param>
    /// <returns>A tuple of the output PDF bytes and the extracted OCR text.</returns>
    /// <exception cref="FailedToGenerateException">PDF generation failed.</exception>
    public Tuple<byte[], string> CreateSearchablePdf(byte[] fileData, PdfMeta metaData, bool firstPageOnly = false)
    {
        if (fileData == null || fileData.Length == 0)
            throw new FailedToGenerateException("No Data in fileData", new ArgumentException(nameof(fileData)));

        string sessionName = TempData.Instance.CreateNewSession();
        try
        {
            int pageCountStart = GetPages(fileData);
            OnCompressorEvent?.Invoke("Created Session:" + sessionName);
            string inputDataFilePath = TempData.Instance.CreateTempFile(sessionName, ".pdf");
            string outputDataFilePath = TempData.Instance.CreateTempFile(sessionName, ".pdf");


            using (FileStream writer = new(inputDataFilePath, FileMode.Create, FileAccess.Write))
            {
                writer.Write(fileData, 0, fileData.Length);
                writer.Flush(true);
                writer.Close();
            }

            bool signed = PdfSigned(inputDataFilePath);


            OnCompressorEvent?.Invoke(sessionName + " Wrote binary to file");
            OnCompressorEvent?.Invoke(sessionName + " Begin Compress and Ocr");
            string pageBody = CompressAndOcr(sessionName, inputDataFilePath, outputDataFilePath, metaData, firstPageOnly);


            if (signed || firstPageOnly)
                return new Tuple<byte[], string>(fileData, pageBody);

            string outputFileName = outputDataFilePath;
            if (PdfSettings.CompressFinalPdf)
            {
                OnCompressorEvent?.Invoke(sessionName + " Compressing output");
                GhostScript gs = new(GhostScriptPath, PdfSettings.Dpi);
                outputFileName = gs.CompressPdf(outputDataFilePath, sessionName, PdfSettings.PdfCompatibilityLevel,
                    PdfSettings.DistillerMode,
                    PdfSettings.DistillerOptions);
            }

            byte[] outFile = File.ReadAllBytes(outputFileName);


            int pageCountEnd = GetPages(outFile);
            OnCompressorEvent?.Invoke(sessionName + " Destroying session");

            if (pageCountEnd != pageCountStart || pageCountEnd == -100 || pageCountStart == -100)
                throw new PageCountMismatchException("Page count is different", pageCountStart, pageCountEnd);
            return new Tuple<byte[], string>(outFile, pageBody);
        }
        catch (Exception e)
        {
            OnExceptionOccurred?.Invoke(this, e);
            throw new FailedToGenerateException("Error in: CreateSearchablePdf", e);
        }
        finally
        {
            SessionsToClean.TryAdd(sessionName, sessionName);
            //TempData.Instance.DestroySession(sessionName);
        }
    }

    /// <summary>
    /// Returns the number of pages in a PDF from its raw bytes.
    /// Returns -100 if the page count cannot be determined.
    /// </summary>
    private int GetPages(byte[] data)
    {
        try
        {
            using (iTextSharp.text.pdf.PdfReader pdfReader = new(data))
                return pdfReader.NumberOfPages;
        }
        catch (Exception)
        {
            return -100;
        }
    }

    public event CompressorEvent OnCompressorEvent;

    public event CompressorExceptionOccurred OnExceptionOccurred;
    public event PreProcessImage OnPreProcessImage;

    /// <summary>
    /// Checks whether a PDF file contains digital signatures.
    /// Returns <c>true</c> if signed or if the file cannot be read.
    /// </summary>
    /// <param name="path">Path to the PDF file to check.</param>
    /// <returns><c>true</c> if the PDF is signed or unreadable; otherwise <c>false</c>.</returns>
    public bool PdfSigned(string path)
    {
        try
        {
            using iTextSharp.text.pdf.PdfReader reader = new(path);
            AcroFields fields = reader.AcroFields;
            return fields.GetSignatureNames().Count > 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// Stops the session cleanup timer and disposes the shared <see cref="TempData"/> instance.
    /// </summary>
    public void Dispose()
    {
        CleanUpTimer.Stop();
        CleanUpTimer.Elapsed -= CleanUpTimer_Elapsed;
        CleanUpTimer.Dispose();
        TempData.Instance.Dispose();
    }
}