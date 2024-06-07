using System.Collections.Concurrent;
using System.Drawing;
using iTextSharp.text.pdf;
using Utility.Hocr.Enums;
using Utility.Hocr.Exceptions;
using Utility.Hocr.ImageProcessors;
using Rectangle = iTextSharp.text.Rectangle;

namespace Utility.Hocr.Pdf;
#pragma warning disable CA1416
public delegate void CompressorExceptionOccurred(PdfCompressor c, Exception x);

public delegate void CompressorEvent(string msg);

public delegate string PreProcessImage(string bitmapPath);





public class PdfCompressor:IDisposable
{
    private System.Timers.Timer CleanUpTimer;

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
                                chk.Dispose();
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

                    reader.Dispose();
                    writer.SaveAndClose();
                    writer.Dispose();
                    return pageBody;
                }
                catch (Exception x)
                {
                    OnCompressorEvent?.Invoke(sessionName + " Image not supported in " +
                                              Path.GetFileName(inputFileName) +
                                              ". Skipping");
                    OnExceptionOccurred?.Invoke(this, x);
                    reader?.Dispose();
                    writer?.SaveAndClose();
                    writer?.Dispose();
                    return pageBody;
                }
            }
        }
    }


    public Tuple<byte[], string> CreateSearchablePdf(byte[] fileData, PdfMeta metaData, bool firstPageOnly = false)
    {
        string sessionName = TempData.Instance.CreateNewSession();
        try
        {
            int pageCountStart = GetPages(fileData);
            OnCompressorEvent?.Invoke("Created Session:" + sessionName);
            string inputDataFilePath = TempData.Instance.CreateTempFile(sessionName, ".pdf");
            string outputDataFilePath = TempData.Instance.CreateTempFile(sessionName, ".pdf");


            if (fileData == null || fileData.Length == 0)
                throw new Exception("No Data in fileData");


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

    public bool PdfSigned(string path)
    {
        try
        {
            iTextSharp.text.pdf.PdfReader reader = new(path);
            AcroFields fields = reader.AcroFields;
            return fields.GetSignatureNames().Count > 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    public void Dispose()
    {
        TempData.Instance.Dispose();
    }
}