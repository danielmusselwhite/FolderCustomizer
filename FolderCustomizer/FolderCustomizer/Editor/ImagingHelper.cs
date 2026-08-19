using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderCustomizer;

public static class ImagingHelper
{
    private const int MaxIconSize = 256;
    private const int IconDirectorySize = 6;
    private const int IconDirectoryEntrySize = 16;

    /// <summary>
    /// Converts an image stream to a single-image ICO file.
    /// </summary>
    /// <param name="input">
    /// Stream containing a supported image such as PNG, JPEG or BMP.
    /// </param>
    /// <param name="output">
    /// Stream to which the ICO file will be written.
    /// </param>
    /// <param name="size">
    /// Maximum icon dimension. Valid values are 1 through 256.
    /// </param>
    /// <param name="preserveAspectRatio">
    /// Whether to preserve the source image's aspect ratio.
    /// </param>
    public static void ConvertToIcon(
        Stream input,
        Stream output,
        int size = MaxIconSize,
        bool preserveAspectRatio = true)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        if (!input.CanRead)
            throw new ArgumentException(
                "The input stream must be readable.",
                nameof(input));

        if (!output.CanWrite)
            throw new ArgumentException(
                "The output stream must be writable.",
                nameof(output));

        if (size is < 1 or > MaxIconSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                $"Icon size must be between 1 and {MaxIconSize} pixels.");
        }

        BitmapSource source = LoadBitmap(input);

        (int width, int height) = CalculateDimensions(
            source.PixelWidth,
            source.PixelHeight,
            size,
            preserveAspectRatio);

        BitmapSource resizedImage = ResizeImage(
            source,
            width,
            height);

        byte[] pngData = EncodeAsPng(resizedImage);

        WriteIcon(
            output,
            pngData,
            width,
            height);
    }

    /// <summary>
    /// Converts an image file to a single-image ICO file.
    /// </summary>
    public static void ConvertToIcon(
        string inputPath,
        string outputPath,
        int size = MaxIconSize,
        bool preserveAspectRatio = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        using FileStream input = new(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        using FileStream output = new(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        ConvertToIcon(
            input,
            output,
            size,
            preserveAspectRatio);
    }

    private static BitmapSource LoadBitmap(Stream stream)
    {
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count == 0)
            throw new InvalidDataException(
                "The image does not contain a valid bitmap frame.");

        BitmapFrame frame = decoder.Frames[0];

        // Convert to a predictable 32-bit format for the icon.
        var converted = new FormatConvertedBitmap(
            frame,
            PixelFormats.Bgra32,
            null,
            0);

        converted.Freeze();

        return converted;
    }

    private static (int Width, int Height) CalculateDimensions(
        int sourceWidth,
        int sourceHeight,
        int size,
        bool preserveAspectRatio)
    {
        if (!preserveAspectRatio)
            return (size, size);

        double scale = Math.Min(
            (double)size / sourceWidth,
            (double)size / sourceHeight);

        int width = Math.Max(
            1,
            (int)Math.Round(sourceWidth * scale));

        int height = Math.Max(
            1,
            (int)Math.Round(sourceHeight * scale));

        return (width, height);
    }

    private static BitmapSource ResizeImage(
        BitmapSource source,
        int width,
        int height)
    {
        if (source.PixelWidth == width &&
            source.PixelHeight == height)
        {
            return source;
        }

        double scaleX =
            (double)width / source.PixelWidth;

        double scaleY =
            (double)height / source.PixelHeight;

        var resized = new TransformedBitmap(
            source,
            new ScaleTransform(scaleX, scaleY));

        resized.Freeze();

        return resized;
    }

    private static byte[] EncodeAsPng(
        BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();

        encoder.Frames.Add(
            BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();

        encoder.Save(stream);

        return stream.ToArray();
    }

    private static void WriteIcon(
        Stream output,
        byte[] pngData,
        int width,
        int height)
    {
        // Width/height use 0 to represent 256 in the ICO format.
        byte iconWidth =
            width == MaxIconSize
                ? (byte)0
                : checked((byte)width);

        byte iconHeight =
            height == MaxIconSize
                ? (byte)0
                : checked((byte)height);

        if (output.CanSeek)
        {
            output.Position = 0;
            output.SetLength(0);
        }

        using var writer = new BinaryWriter(
            output,
            System.Text.Encoding.UTF8,
            leaveOpen: true);

        // ICONDIR
        writer.Write((ushort)0); // Reserved
        writer.Write((ushort)1); // Type: icon
        writer.Write((ushort)1); // Number of images

        // ICONDIRENTRY
        writer.Write(iconWidth);
        writer.Write(iconHeight);

        writer.Write((byte)0);   // Colour palette count
        writer.Write((byte)0);   // Reserved

        writer.Write((ushort)1); // Colour planes
        writer.Write((ushort)32); // Bits per pixel

        writer.Write((uint)pngData.Length);

        writer.Write(
            (uint)(IconDirectorySize +
                   IconDirectoryEntrySize));

        // PNG image data
        writer.Write(pngData);

        writer.Flush();
    }
}