using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderCustomizer.Services;

/// <summary>
/// Provides operations for reading, applying, and removing custom Windows folder icons.
/// </summary>
/// <remarks>
/// Windows folder customisation is implemented by creating a <c>desktop.ini</c> file
/// containing shell icon metadata and marking the target folder with the
/// <see cref="FileAttributes.System"/> attribute.
///
/// This service also notifies the Windows Shell when folder icon metadata changes so
/// that File Explorer can refresh its cached representation of the folder.
/// </remarks>
public sealed class FolderIconService
{
    #region Windows Shell Constants

    /// <summary>
    /// Indicates to the Windows Shell that an existing item has been updated.
    /// </summary>
    private const uint ShcneUpdateItem = 0x00002000;

    /// <summary>
    /// Indicates that shell notification item arguments are Unicode file-system paths.
    /// </summary>
    private const uint ShcnfPathW = 0x0005;

    #endregion

    #region Public Operations

    /// <summary>
    /// Gets the icon currently representing the specified folder.
    /// </summary>
    /// <param name="folderPath">
    /// The path of the folder whose icon should be retrieved.
    /// </param>
    /// <returns>
    /// The folder's custom icon when a valid FolderCustomizer style is present;
    /// otherwise, the standard Windows folder icon.
    /// </returns>
    /// <remarks>
    /// Custom icons are centred on a square canvas so that they can be displayed
    /// consistently alongside the standard folder preview used by the editor.
    ///
    /// If the custom icon cannot be decoded, the method safely falls back to the
    /// default Windows folder icon.
    /// </remarks>
    public BitmapSource GetFolderIcon(string folderPath)
    {
        string customIconPath = Path.Combine(folderPath, "custom_icon.ico");

        if (HasCustomStyle(folderPath) && File.Exists(customIconPath))
        {
            try
            {
                BitmapSource source = LoadIconFile(customIconPath);
                return NormalizeIconPosition(source);
            }
            catch
            {
                // A corrupt or unsupported custom icon should not prevent the folder
                // from being displayed in the editor. Fall back to the Windows default.
            }
        }

        return WindowsFolderIconProvider.GetDefaultFolderIcon();
    }

    /// <summary>
    /// Determines whether the specified folder contains a FolderCustomizer-managed style.
    /// </summary>
    /// <param name="folderPath">
    /// The path of the folder to inspect.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when both the generated icon and <c>desktop.ini</c>
    /// exist and the configuration references <c>custom_icon.ico</c>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The presence of the files alone is not considered sufficient. The
    /// <c>desktop.ini</c> contents are also inspected to avoid treating unrelated
    /// folder customisations as styles created by this application.
    /// </remarks>
    public bool HasCustomStyle(string folderPath)
    {
        string iconPath = Path.Combine(folderPath, "custom_icon.ico");
        string desktopIniPath = Path.Combine(folderPath, "desktop.ini");

        if (!File.Exists(iconPath) || !File.Exists(desktopIniPath))
            return false;

        try
        {
            string desktopIni = File.ReadAllText(desktopIniPath);

            return desktopIni.Contains(
                "custom_icon.ico",
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If the configuration cannot be read, treat the folder as unmanaged
            // rather than allowing a file-system error to break the editor workflow.
            return false;
        }
    }

    /// <summary>
    /// Applies the supplied icon file as the custom icon for a folder.
    /// </summary>
    /// <param name="folderPath">
    /// The path of the folder that should receive the custom icon.
    /// </param>
    /// <param name="iconPath">
    /// The path of the ICO file that should be referenced by the folder's
    /// <c>desktop.ini</c> configuration.
    /// </param>
    /// <remarks>
    /// Applying a Windows folder icon requires three main steps:
    /// creating the shell metadata in <c>desktop.ini</c>, assigning the required
    /// file-system attributes, and notifying the Windows Shell that the folder changed.
    ///
    /// Any existing <c>desktop.ini</c> file is replaced by the configuration generated
    /// by FolderCustomizer.
    /// </remarks>
    public void ApplyCustomIcon(string folderPath, string iconPath)
    {
        string desktopIniPath = Path.Combine(folderPath, "desktop.ini");

        // Windows only processes folder customisation metadata when the folder carries
        // the System attribute.
        FileAttributes folderAttributes = File.GetAttributes(folderPath);
        File.SetAttributes(
            folderPath,
            folderAttributes | FileAttributes.System);

        // desktop.ini may already be hidden/system-protected, so normalise its
        // attributes before replacing it.
        if (File.Exists(desktopIniPath))
        {
            File.SetAttributes(desktopIniPath, FileAttributes.Normal);
            File.Delete(desktopIniPath);
        }

        string desktopIni =
            "[.ShellClassInfo]\r\n" +
            $"IconResource={Path.GetFileName(iconPath)},0\r\n";

        // Windows Shell metadata is traditionally stored as Unicode text.
        File.WriteAllText(
            desktopIniPath,
            desktopIni,
            Encoding.Unicode);

        File.SetAttributes(
            desktopIniPath,
            FileAttributes.Hidden | FileAttributes.System);

        File.SetAttributes(
            iconPath,
            File.GetAttributes(iconPath) | FileAttributes.Hidden);

        RefreshShell(folderPath);
    }

    /// <summary>
    /// Removes the custom icon and FolderCustomizer metadata from the specified folder.
    /// </summary>
    /// <param name="folderPath">
    /// The path of the folder whose custom style should be removed.
    /// </param>
    /// <remarks>
    /// The generated icon and <c>desktop.ini</c> files are removed, the folder's
    /// <see cref="FileAttributes.System"/> attribute is cleared, and the Windows Shell
    /// is notified so that File Explorer can refresh the folder.
    /// </remarks>
    public void ClearCustomStyle(string folderPath)
    {
        string iconPath = Path.Combine(folderPath, "custom_icon.ico");
        string desktopIniPath = Path.Combine(folderPath, "desktop.ini");

        if (File.Exists(iconPath))
        {
            // Hidden files should be normalised before deletion to avoid attribute-related
            // file-system issues.
            File.SetAttributes(iconPath, FileAttributes.Normal);
            File.Delete(iconPath);
        }

        if (File.Exists(desktopIniPath))
        {
            File.SetAttributes(desktopIniPath, FileAttributes.Normal);
            File.Delete(desktopIniPath);
        }

        FileAttributes attributes = File.GetAttributes(folderPath);
        attributes &= ~FileAttributes.System;

        File.SetAttributes(folderPath, attributes);

        RefreshShell(folderPath);
    }

    #endregion

    #region Icon Loading

    /// <summary>
    /// Loads the largest available image frame from an ICO file.
    /// </summary>
    /// <param name="iconPath">
    /// The path of the icon file to decode.
    /// </param>
    /// <returns>
    /// The largest decoded frame contained within the icon file.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the icon contains no usable image frames.
    /// </exception>
    /// <remarks>
    /// ICO files can contain multiple resolutions. The frame with the greatest
    /// pixel area is selected so the editor uses the highest-resolution representation
    /// available.
    ///
    /// <see cref="BitmapCacheOption.OnLoad"/> ensures that the bitmap data is fully
    /// loaded before the underlying file stream is disposed.
    /// </remarks>
    private static BitmapSource LoadIconFile(string iconPath)
    {
        using FileStream stream = new(
            iconPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        BitmapDecoder decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count == 0)
        {
            throw new InvalidDataException(
                "The icon does not contain any image frames.");
        }

        BitmapFrame? largestFrame = null;

        foreach (BitmapFrame frame in decoder.Frames)
        {
            if (largestFrame is null ||
                frame.PixelWidth * frame.PixelHeight >
                largestFrame.PixelWidth * largestFrame.PixelHeight)
            {
                largestFrame = frame;
            }
        }

        if (largestFrame is null)
        {
            throw new InvalidDataException(
                "The icon does not contain a valid image frame.");
        }

        largestFrame.Freeze();

        return largestFrame;
    }

    /// <summary>
    /// Centres an image on a square transparent canvas while preserving its aspect ratio.
    /// </summary>
    /// <param name="source">
    /// The source image to centre.
    /// </param>
    /// <param name="canvasSize">
    /// The width and height, in pixels, of the output canvas.
    /// </param>
    /// <returns>
    /// A square bitmap containing the source image scaled proportionally and centred.
    /// </returns>
    /// <summary>
    /// Centers the visible portion of an icon on a fixed-size transparent canvas
    /// without enlarging the source artwork. Icons larger than the available
    /// canvas are scaled down proportionally.
    /// </summary>
    /// <param name="source">The source bitmap to normalize.</param>
    /// <param name="canvasSize">The width and height of the output canvas.</param>
    /// <param name="padding">The minimum padding to preserve around the icon.</param>
    /// <returns>A centered bitmap with the original icon scale preserved where possible.</returns>
    private static BitmapSource NormalizeIconPosition(BitmapSource source, int canvasSize = 256)
    {
        BitmapSource converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;

        byte[] pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * stride) + (x * 4);
                byte alpha = pixels[index + 3];

                if (alpha <= 8)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            return source;

        double visibleCentreX = (minX + maxX) / 2.0;
        double visibleCentreY = (minY + maxY) / 2.0;

        double targetCentre = canvasSize / 2.0;

        double offsetX = targetCentre - visibleCentreX;
        double offsetY = targetCentre - visibleCentreY;

        var visual = new DrawingVisual();

        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawImage(
                converted,
                new Rect(offsetX, offsetY, width, height));
        }

        var result = new RenderTargetBitmap(
            canvasSize,
            canvasSize,
            96,
            96,
            PixelFormats.Pbgra32);

        result.Render(visual);
        result.Freeze();

        return result;
    }

    #endregion

    #region Windows Shell Integration

    /// <summary>
    /// Notifies the Windows Shell that a folder's visual metadata has changed.
    /// </summary>
    /// <param name="folderPath">
    /// The path of the folder that should be refreshed.
    /// </param>
    private static void RefreshShell(string folderPath)
    {
        SHChangeNotify(
            ShcneUpdateItem,
            ShcnfPathW,
            folderPath,
            null);
    }

    /// <summary>
    /// Sends a change notification to the Windows Shell.
    /// </summary>
    /// <param name="eventId">
    /// The type of shell event being reported.
    /// </param>
    /// <param name="flags">
    /// Flags describing how the item parameters should be interpreted.
    /// </param>
    /// <param name="item1">
    /// The primary item associated with the shell event.
    /// </param>
    /// <param name="item2">
    /// An optional secondary item associated with the shell event.
    /// </param>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(
        uint eventId,
        uint flags,
        string? item1,
        string? item2);

    #endregion
}