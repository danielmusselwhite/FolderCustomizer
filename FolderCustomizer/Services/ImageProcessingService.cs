using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderCustomizer.Services;

public sealed class ImageProcessingService
{
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

                        byte blue = pixel[0];
                        byte green = pixel[1];
                        byte red = pixel[2];

                        double intensity = ((0.299 * red) + (0.587 * green) + (0.114 * blue)) / 255.0;

                        pixel[0] = (byte)(color.B * intensity);
                        pixel[1] = (byte)(color.G * intensity);
                        pixel[2] = (byte)(color.R * intensity);
                    }
                }
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
        }
        finally
        {
            bitmap.Unlock();
        }

        bitmap.Freeze();

        return bitmap;
    }
}