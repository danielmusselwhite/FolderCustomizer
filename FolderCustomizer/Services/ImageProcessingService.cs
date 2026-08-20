using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderCustomizer.Services;

/// <summary>
/// Provides image-processing operations used by the folder icon editor.
/// </summary>
/// <remarks>
/// The service currently supports recolouring folder artwork while preserving the
/// perceived brightness and transparency of the original image.
/// </remarks>
public sealed class ImageProcessingService
{
    #region Colour Processing

    /// <summary>
    /// Recolours the supplied bitmap using the specified colour while preserving
    /// the original image's relative brightness.
    /// </summary>
    /// <param name="source">
    /// The source bitmap to recolour.
    /// </param>
    /// <param name="color">
    /// The colour that should be applied to the source image.
    /// </param>
    /// <returns>
    /// A frozen <see cref="WriteableBitmap"/> containing the recoloured image.
    /// </returns>
    /// <remarks>
    /// Each pixel is converted to a luminance value using weighted red, green,
    /// and blue components. That luminance is then used to scale the supplied
    /// colour, preserving highlights and shadows from the original artwork.
    ///
    /// The alpha channel is intentionally left unchanged so transparent and
    /// partially transparent areas retain their original opacity.
    ///
    /// Direct back-buffer access is used for efficient per-pixel modification.
    /// </remarks>
    public WriteableBitmap ApplyColor(BitmapSource source, Color color)
    {
        var bitmap = new WriteableBitmap(source);

        bitmap.Lock();

        try
        {
            unsafe
            {
                byte* buffer = (byte*)bitmap.BackBuffer.ToPointer();

                int stride = bitmap.BackBufferStride;
                int width = bitmap.PixelWidth;
                int height = bitmap.PixelHeight;

                for (int y = 0; y < height; y++)
                {
                    byte* row = buffer + (y * stride);

                    for (int x = 0; x < width; x++)
                    {
                        byte* pixel = row + (x * 4);

                        // WPF stores pixels in BGRA order for this bitmap format.
                        byte blue = pixel[0];
                        byte green = pixel[1];
                        byte red = pixel[2];

                        // Convert the original RGB values to perceived luminance so
                        // the recoloured image keeps its existing highlights and shadows.
                        double intensity =
                            ((0.299 * red) +
                             (0.587 * green) +
                             (0.114 * blue)) / 255.0;

                        pixel[0] = (byte)(color.B * intensity);
                        pixel[1] = (byte)(color.G * intensity);
                        pixel[2] = (byte)(color.R * intensity);

                        // pixel[3] is the alpha channel and is deliberately preserved.
                    }
                }
            }

            // Inform WPF that the entire bitmap has been modified.
            bitmap.AddDirtyRect(
                new Int32Rect(
                    0,
                    0,
                    bitmap.PixelWidth,
                    bitmap.PixelHeight));
        }
        finally
        {
            bitmap.Unlock();
        }

        // The processed image is immutable after creation and can therefore be
        // safely shared across WPF threads where appropriate.
        bitmap.Freeze();

        return bitmap;
    }

    #endregion
}