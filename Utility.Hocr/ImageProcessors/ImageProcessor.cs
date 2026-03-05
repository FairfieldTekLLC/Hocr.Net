using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Utility.Hocr.Exceptions;
#pragma warning disable CA1416

namespace Utility.Hocr.ImageProcessors;

/// <summary>
/// Provides image conversion and processing utilities for PDF generation,
/// including format conversion, bitonal thresholding, and resolution adjustment.
/// </summary>
internal class ImageProcessor
{
    /// <summary>
    /// Converts a bitmap to a 1-bit-per-pixel bitonal (black and white) image
    /// using a fixed brightness threshold of 580 (sum of R+G+B channels).
    /// </summary>
    /// <param name="original">The source bitmap to convert.</param>
    /// <returns>A new <see cref="Bitmap"/> in <see cref="PixelFormat.Format1bppIndexed"/> format.</returns>
    public static Bitmap ConvertToBitonal(Bitmap original)
    {
        Bitmap source;
        // If original bitmap is not already in 32 BPP, ARGB format, then convert
        if (original.PixelFormat != PixelFormat.Format32bppArgb)
        {
            source = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
            source.SetResolution(original.HorizontalResolution, original.VerticalResolution);
            using (Graphics g = Graphics.FromImage(source)) g.DrawImageUnscaled(original, 0, 0);
        }
        else
        {
            source = original;
        }

        // Lock source bitmap in memory
        BitmapData sourceData = source.LockBits(new Rectangle(0, 0, source.Width, source.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        // Copy image data to binary array
        int imageSize = sourceData.Stride * sourceData.Height;
        byte[] sourceBuffer = new byte[imageSize];
        Marshal.Copy(sourceData.Scan0, sourceBuffer, 0, imageSize);

        // Unlock source bitmap
        source.UnlockBits(sourceData);

        // Create destination bitmap
        Bitmap destination = new(source.Width, source.Height, PixelFormat.Format1bppIndexed);
        destination.SetResolution(original.HorizontalResolution, original.VerticalResolution);
        // destination.SetResolution(200,200);

        // Lock destination bitmap in memory
        BitmapData destinationData = destination.LockBits(new Rectangle(0, 0, destination.Width, destination.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format1bppIndexed);

        // Create destination buffer
        imageSize = destinationData.Stride * destinationData.Height;
        byte[] destinationBuffer = new byte[imageSize];

        int height = source.Height;
        int width = source.Width;
        const int threshold = 580;

        // Iterate lines
        for (int y = 0; y < height; y++)
        {
            int sourceIndex = y * sourceData.Stride;
            int destinationIndex = y * destinationData.Stride;
            byte destinationValue = 0;
            int pixelValue = 128;

            // Iterate pixels
            for (int x = 0; x < width; x++)
            {
                // Compute pixel brightness (i.e. total of Red, Green, and Blue values) - Thanks murx
                //                           B                             G                              R
                int pixelTotal = sourceBuffer[sourceIndex] + sourceBuffer[sourceIndex + 1] +
                                 sourceBuffer[sourceIndex + 2];
                if (pixelTotal > threshold)
                    destinationValue += (byte) pixelValue;
                if (pixelValue == 1)
                {
                    destinationBuffer[destinationIndex] = destinationValue;
                    destinationIndex++;
                    destinationValue = 0;
                    pixelValue = 128;
                }
                else
                {
                    pixelValue >>= 1;
                }

                sourceIndex += 4;
            }

            if (pixelValue != 128)
                destinationBuffer[destinationIndex] = destinationValue;
        }

        // Copy binary image data to destination bitmap
        Marshal.Copy(destinationBuffer, 0, destinationData.Scan0, imageSize);

        // Unlock destination bitmap
        destination.UnlockBits(destinationData);

        // Dispose of source if not originally supplied bitmap
        if (source != original)
            source.Dispose();

        // Return
        return destination;
    }


    /// <summary>
    /// Converts an image to a CCITT Group 4 fax-compressed TIFF, suitable
    /// for efficient storage of bitonal document images.
    /// </summary>
    /// <param name="image">The source image to convert.</param>
    /// <param name="dpi">The resolution in dots per inch for the output image.</param>
    /// <returns>A TIFF-encoded <see cref="Image"/> with CCITT4 compression.</returns>
    public static Image ConvertToCcittFaxTiff(Image image, int dpi)
    {
        ImageCodecInfo codecInfo = GetCodecInfoForName("TIFF");
        EncoderParameters encoderParams = new(2)
        {
            Param =
            {
                [0] = new EncoderParameter(Encoder.Quality, 08L),
                [1] = new EncoderParameter(Encoder.SaveFlag, (long) EncoderValue.CompressionCCITT4)
            }
        };

        Bitmap bmg = GetAsBitmap(image, dpi);
        Bitmap bitonalBmp = ConvertToBitonal(bmg);
        MemoryStream ms = new();
        bitonalBmp.Save(ms, codecInfo, encoderParams);
        bitonalBmp.Dispose();
        bmg.Dispose();
        return Image.FromStream(ms);
    }

    /// <summary>
    /// Converts an image to the specified format at the given quality and resolution.
    /// </summary>
    /// <param name="imageToConvert">The source image to convert.</param>
    /// <param name="codecName">The image codec format name (e.g., "PNG", "JPEG", "BMP").</param>
    /// <param name="quality">The encoding quality level (0–100).</param>
    /// <param name="dpi">The resolution in dots per inch for the output image.</param>
    /// <returns>A new <see cref="Image"/> encoded in the specified format.</returns>
    public static Image ConvertToImage(Image imageToConvert, string codecName, long quality, int dpi)
    {
        ImageCodecInfo codecInfo = GetCodecInfoForName(codecName);
        EncoderParameters encoderParams = new(1) {Param = {[0] = new EncoderParameter(Encoder.Quality, quality)}};

        Bitmap bmp = GetAsBitmap(imageToConvert, dpi);
        Bitmap newBitmap = new(bmp);
        newBitmap.SetResolution(dpi, dpi);
        MemoryStream ms = new();
        newBitmap.Save(ms, codecInfo, encoderParams);
        bmp.Dispose();
        newBitmap.Dispose();
        return Image.FromStream(ms);
    }


    /// <summary>
    /// Creates a 24-bit RGB bitmap copy of the given image at the specified resolution.
    /// </summary>
    /// <param name="image">The source image to copy.</param>
    /// <param name="dpi">The resolution in dots per inch for the output bitmap.</param>
    /// <returns>A new <see cref="Bitmap"/> in <see cref="PixelFormat.Format24bppRgb"/> format.</returns>
    /// <exception cref="InvalidBitmapException">The image could not be converted.</exception>
    public static Bitmap GetAsBitmap(Image image, int dpi)
    {
        try
        {
            Bitmap bmp = new(image.Width, image.Height, PixelFormat.Format24bppRgb);
            bmp.SetResolution(dpi, dpi);
            using (Graphics g = Graphics.FromImage(bmp))
                g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));

            return bmp;
        }
        catch (Exception e)
        {
            throw new InvalidBitmapException(" Hocr.ImageProcessors.ImageProcessor - GetAsBitmap", e);
        }
    }


    /// <summary>
    /// Retrieves the <see cref="ImageCodecInfo"/> for the encoder matching the specified format name.
    /// </summary>
    /// <param name="codecType">The format description to match (e.g., "JPEG", "PNG", "BMP", "TIFF").</param>
    /// <returns>The matching <see cref="ImageCodecInfo"/>, or <c>null</c> if not found.</returns>
    public static ImageCodecInfo GetCodecInfoForName(string codecType)
    {
        ImageCodecInfo[] info = ImageCodecInfo.GetImageEncoders();
        return info.FirstOrDefault(t => t.FormatDescription.Equals(codecType));
    }
}