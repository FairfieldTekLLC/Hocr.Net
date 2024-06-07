# Hocr.Net
DotNet version of Hocr

C# Library for converting PDF files to Searchable PDF Files

* Need to batch convert 100 of scan PDF's to Searchable PFS's?
* Don't want to pay thousands of dollars for a component?

I have personally tested this library with over 110 thousand PDFs.  Beyond a few fringe cases the code has performed as it was designed..  I was able to process 110k pdfs (Some hundreds of pages) over a 3 day period using 5 servers.

Internally, Hocr uses Tesseract, GhostScript, iTextSharp and the HtmlAgilityPack.  Please check the licensing for each nuget to make sure you are in compliance.

This library IS THREADSAFE so you can process multiple PDF's at the same time in different threads, you do not need to process them one at a time.

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
```C#
