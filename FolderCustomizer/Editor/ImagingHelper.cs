using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace FolderCustomizer;

/// <summary>
/// Provides image conversion utilities used to generate Windows ICO files.
/// </summary>
/// <remarks>
/// The helper converts an already prepared image into a single-image ICO file
/// containing PNG-encoded image data. No resizing or resampling is performed.
/// </remarks>
public static class ImagingHelper
{
    #region ICO Format Constants

    private const int MaxIconSize = 256;
    private const int IconDirectorySize = 6;
    private const int IconDirectoryEntrySize = 16;

    #endregion

    #region Public API

    /// <summary>
    /// Converts an image stream into a single-image ICO file without resizing
    /// or resampling the source image.
    /// </summary>
    /// <param name="input">A readable stream containing the source image.</param>
    /// <param name="output">A writable stream to which the ICO file will be written.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="input"/> or <paramref name="output"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the input stream is unreadable, the output stream is unwritable,
    /// or the source image dimensions are unsupported.
    /// </exception>
    public static void ConvertToIcon(Stream input, Stream output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        if (!input.CanRead)
            throw new ArgumentException("The input stream must be readable.", nameof(input));

        if (!output.CanWrite)
            throw new ArgumentException("The output stream must be writable.", nameof(output));

        BitmapFrame frame = LoadBitmap(input);

        ValidateDimensions(frame.PixelWidth, frame.PixelHeight);

        byte[] pngData = EncodeAsPng(frame);

        WriteIcon(output, pngData, frame.PixelWidth, frame.PixelHeight);
    }

    /// <summary>
    /// Converts an image file into a single-image ICO file without resizing
    /// or resampling the source image.
    /// </summary>
    /// <param name="inputPath">The path of the source image file.</param>
    /// <param name="outputPath">The path where the generated ICO file should be written.</param>
    public static void ConvertToIcon(string inputPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        using FileStream input = File.OpenRead(inputPath);
        using FileStream output = File.Create(outputPath);

        ConvertToIcon(input, output);
    }

    #endregion

    #region Bitmap Preparation

    /// <summary>
    /// Decodes the first bitmap frame from an image stream.
    /// </summary>
    /// <param name="stream">The stream containing the source image.</param>
    /// <returns>The decoded bitmap frame.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the stream does not contain a valid bitmap frame.
    /// </exception>
    private static BitmapFrame LoadBitmap(Stream stream)
    {
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count == 0)
            throw new InvalidDataException("The image does not contain a valid bitmap frame.");

        return decoder.Frames[0];
    }

    /// <summary>
    /// Validates that the source image dimensions can be represented by this ICO writer.
    /// </summary>
    /// <param name="width">The source image width.</param>
    /// <param name="height">The source image height.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when either dimension falls outside the supported range of 1 to 256 pixels.
    /// </exception>
    private static void ValidateDimensions(int width, int height)
    {
        if (width is < 1 or > MaxIconSize)
            throw new ArgumentException($"Icon width must be between 1 and {MaxIconSize} pixels. Actual width was {width}.");

        if (height is < 1 or > MaxIconSize)
            throw new ArgumentException($"Icon height must be between 1 and {MaxIconSize} pixels. Actual height was {height}.");
    }

    #endregion

    #region PNG Encoding

    /// <summary>
    /// Encodes a bitmap as PNG data without changing its dimensions.
    /// </summary>
    /// <param name="bitmap">The bitmap to encode.</param>
    /// <returns>The complete PNG representation of the bitmap.</returns>
    private static byte[] EncodeAsPng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);

        return stream.ToArray();
    }

    #endregion

    #region ICO Writing

    /// <summary>
    /// Writes a single PNG image into a valid ICO container.
    /// </summary>
    /// <param name="output">The stream that will receive the ICO data.</param>
    /// <param name="pngData">The PNG-encoded image payload.</param>
    /// <param name="width">The width of the icon image.</param>
    /// <param name="height">The height of the icon image.</param>
    private static void WriteIcon(Stream output, byte[] pngData, int width, int height)
    {
        byte iconWidth = width == MaxIconSize ? (byte)0 : checked((byte)width);
        byte iconHeight = height == MaxIconSize ? (byte)0 : checked((byte)height);

        if (output.CanSeek)
        {
            output.Position = 0;
            output.SetLength(0);
        }

        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);

        writer.Write(iconWidth);
        writer.Write(iconHeight);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)pngData.Length);
        writer.Write((uint)(IconDirectorySize + IconDirectoryEntrySize));

        writer.Write(pngData);
        writer.Flush();
    }

    #endregion
}