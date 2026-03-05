# Hocr.Net
DotNet version of Hocr

C# Library for converting PDF files to Searchable PDF Files

* Need to batch convert 100 of scan PDF's to Searchable PFS's?
* Don't want to pay thousands of dollars for a component?

I have personally tested this library with over 110 thousand PDFs.  Beyond a few fringe cases the code has performed as it was designed..  I was able to process 110k pdfs (Some hundreds of pages) over a 3 day period using 5 servers.

Internally, Hocr uses Tesseract, GhostScript, iTextSharp and the HtmlAgilityPack.  Please check the licensing for each nuget to make sure you are in compliance.

This library IS THREADSAFE so you can process multiple PDF's at the same time in different threads, you do not need to process them one at a time.

---

## Recent Changes

### Thread Safety & Concurrency Fixes

- **`TempData._caches`** — Replaced `Dictionary<string, string>` with `ConcurrentDictionary<string, string>` to prevent data corruption under concurrent access from `Parallel.ForEach` and background timer threads.
- **`TempData.DestroySession`** — Replaced three separate check-then-act operations (TOCTOU race) with a single atomic `TryRemove` call.
- **`TempData._cleanUpTimerRunning`** — Replaced non-volatile `bool` with `Interlocked.CompareExchange` to guarantee visibility across threads.
- **`TempData.Dispose`** — Added double-dispose guard (`Interlocked.CompareExchange`), spin-waits for any in-flight timer callback to complete before running cleanup, and unsubscribes the timer event.
- **`TempData` public methods** — Added `ObjectDisposedException` guards to `CreateNewSession`, `CreateTempFile`, and `CreateDirectory`.
- **`PdfCompressor.Dispose`** — Now stops and disposes its own `CleanUpTimer` (previously leaked).

### Bug Fixes

- **`OcrController.CreateHocr`** — Fixed hardcoded `"eng"` language; now correctly passes the `language` parameter to the Tesseract engine.
- **`PdfCompressor.CreateSearchablePdf`** — Moved null/empty validation of `fileData` before session creation and `GetPages` call (previously validated after use).
- **`PdfCompressor.PdfSigned`** — Added `using` declaration for `iTextSharp.text.pdf.PdfReader` (file handle was leaked on every call).

### Resource Leak Fixes

- **`PdfCreator.WritePageDrawBlocks`** — Two `Graphics` objects and four `Pen` objects were never disposed. Consolidated to a single `Graphics` and wrapped all GDI+ objects in `using` statements.
- **`GhostScript.RunCommand`** — Redirected stdout/stderr were never drained, which can deadlock the process when GhostScript output fills the OS pipe buffer. Added `BeginOutputReadLine()`/`BeginErrorReadLine()` to drain streams asynchronously.

### Performance Improvements

- **`PdfCreator.WriteDirectContent`** — `BaseFont.CreateFont()` was called inside the per-line loop (identical result each time). Hoisted to a single call before the loop.
- **`PdfCreator.WriteUnderlayContent`** — `BaseFont.CreateFont()` was called inside the per-**word** loop. Hoisted to a single call before the loop.
- **`TempData.CleanUpFiles`** — Previously processed only one directory per 5-second timer tick and halted the entire batch on the first locked directory. Now processes all queued items per tick, skipping locked directories and re-enqueuing them individually.
- **`TempData.CreateNewSession`** — Eliminated per-call `Regex` allocation and redundant `Path.Combine` computations. Uses `Guid.ToString("N")` for filesystem-safe names directly.
- **`TempData.CreateTempFile`** — Replaced `DateTime.Now.Second + Millisecond` (collision-prone) with `Guid.NewGuid()` for unique filenames.
- **`TempData.Dispose`** — Retry loop now sleeps only after failures instead of before every attempt.
- **`TempData` singleton** — Replaced `Activator.CreateInstance` reflection with direct constructor call.

### Code Quality

- **Redundant `Dispose` calls** — Removed `chk.Dispose()`, `reader.Dispose()`, and `writer.Dispose()` calls inside `using` blocks in `PdfCompressor.CompressAndOcr`.
- **Redundant process cleanup** — Removed `proc.Close()`/`proc.Dispose()` inside `using` block and simplified `while (!HasExited) { WaitForExit(10000) }` loop to a single `WaitForExit()` call in `GhostScript.RunCommand`.
- **Simplified LINQ** — Replaced verbose query syntax with `FirstOrDefault` in `ImageProcessor.GetCodecInfoForName`.
- **XML documentation** — Added XML doc comments to all public APIs and key internal methods across the solution.

---

## Use Hocr!

Example Usage:
```C#
// See https://aka.ms/new-console-template for more information

using Utility.Hocr.Enums;
using Utility.Hocr.Pdf;

const string ghostScriptPathToExecutable = @"C:\gs10.03.1\bin\gswin64c.exe";

static void Comp_OnCompressorEvent(string msg)
{
    Console.WriteLine(msg);
}

Console.WriteLine("Hello, World!");
PdfCompressor comp;
List<string> DistillerOptions = new()
{
    "-dSubsetFonts=true",
    "-dCompressFonts=true",
    "-sProcessColorModel=DeviceRGB",
    "-sColorConversionStrategy=sRGB",
    "-sColorConversionStrategyForImages=sRGB",
    "-dConvertCMYKImagesToRGB=true",
    "-dDetectDuplicateImages=true",
    "-dDownsampleColorImages=false",
    "-dDownsampleGrayImages=false",
    "-dDownsampleMonoImages=false",
    "-dColorImageResolution=265",
    "-dGrayImageResolution=265",
    "-dMonoImageResolution=265",
    "-dDoThumbnails=false",
    "-dCreateJobTicket=false",
    "-dPreserveEPSInfo=false",
    "-dPreserveOPIComments=false",
    "-dPreserveOverprintSettings=false",
    "-dUCRandBGInfo=/Remove"
};

using (comp = new PdfCompressor(ghostScriptPathToExecutable, new PdfCompressorSettings
    {
        PdfCompatibilityLevel = PdfCompatibilityLevel.Acrobat_7_1_6,
        WriteTextMode = WriteTextMode.Word,
        Dpi = 400,
        ImageType = PdfImageType.Jpg,
        ImageQuality = 100,
        CompressFinalPdf = true,
        DistillerMode = dPdfSettings.prepress,
        DistillerOptions = string.Join(" ", DistillerOptions.ToArray())
    }))
{
    comp.OnCompressorEvent += Comp_OnCompressorEvent;
    Parallel.ForEach(Directory.GetFiles("C:\\pdfin"), file =>
        {
            byte[] data = File.ReadAllBytes(file);
            Tuple<byte[], string> result = comp.CreateSearchablePdf(data, new PdfMeta());
            File.WriteAllBytes("c:\\PDFOUT\\" + Path.GetFileName(file), result.Item1);
        }
    );
}
```
