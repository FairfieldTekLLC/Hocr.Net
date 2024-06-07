using System.Drawing;
using System.Reflection;
using Tesseract;
using Utility.Hocr.HocrElements;
using Utility.Hocr.ImageProcessors;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

namespace Utility.Hocr;
#pragma warning disable CA1416
internal class OcrController
{
    internal void AddToDocument(string language, Image image, ref HDocument doc, string sessionName)
    {
        Bitmap b = ImageProcessor.GetAsBitmap(image, (int) Math.Ceiling(image.HorizontalResolution));
        string imageFile = TempData.Instance.CreateTempFile(sessionName, ".tif");
        b.Save(imageFile, ImageFormat.Tiff);
        string result = CreateHocr(language, imageFile, sessionName);
        doc.AddFile(result);
        b.Dispose();
    }

    public string CreateHocr(string language, string imagePath, string sessionName)
    {
        //   Assembly shellViewLibrary = Assembly.LoadFrom(Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "x64", "liblept1753.dll")));


        string dataFolder = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
        string dataPath = TempData.Instance.CreateDirectory(sessionName, dataFolder);
        string outputFile = Path.Combine(dataPath, Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));

        string enginePath = string.Empty;

        if (Assembly.GetEntryAssembly() != null)
            enginePath =
                Path.Combine(
                    Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location) ??
                    throw new InvalidOperationException(), "tessdata");
        else
            enginePath = Path.Combine(Environment.CurrentDirectory, "tessdata");


        using (TesseractEngine engine = new(enginePath, "eng"))
        using (Pix img = Pix.LoadFromFile(imagePath))
        using (Page page = engine.Process(img))
        {
            string hocrtext = page.GetHOCRText(0);
            File.WriteAllText(outputFile + ".hocr", hocrtext);
        }

        return outputFile + ".hocr";
    }
}