using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media.Imaging;

namespace FolderCustomizer.Services;

public sealed class FolderIconService
{
    private const uint ShcneUpdateItem = 0x00002000;
    private const uint ShcnfPathW = 0x0005;

    public BitmapSource GetFolderIcon(string folderPath)
    {
        string customIconPath = Path.Combine(folderPath, "custom_icon.ico");

        if (HasCustomStyle(folderPath) && File.Exists(customIconPath))
        {
            try
            {
                return LoadIconFile(customIconPath);
            }
            catch
            {
                // Fall back to the Windows default icon.
            }
        }

        return WindowsFolderIconProvider.GetDefaultFolderIcon();
    }

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
            return false;
        }
    }

    public void ApplyCustomIcon(string folderPath, string iconPath)
    {
        string desktopIniPath = Path.Combine(folderPath, "desktop.ini");

        FileAttributes folderAttributes = File.GetAttributes(folderPath);
        File.SetAttributes(folderPath, folderAttributes | FileAttributes.System);

        if (File.Exists(desktopIniPath))
        {
            File.SetAttributes(desktopIniPath, FileAttributes.Normal);
            File.Delete(desktopIniPath);
        }

        string desktopIni =
            "[.ShellClassInfo]\r\n" +
            $"IconResource={Path.GetFileName(iconPath)},0\r\n";

        File.WriteAllText(desktopIniPath, desktopIni, Encoding.Unicode);

        File.SetAttributes(
            desktopIniPath,
            FileAttributes.Hidden | FileAttributes.System);

        File.SetAttributes(
            iconPath,
            File.GetAttributes(iconPath) | FileAttributes.Hidden);

        RefreshShell(folderPath);
    }

    public void ClearCustomStyle(string folderPath)
    {
        string iconPath = Path.Combine(folderPath, "custom_icon.ico");
        string desktopIniPath = Path.Combine(folderPath, "desktop.ini");

        if (File.Exists(iconPath))
        {
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
            throw new InvalidDataException("The icon does not contain any image frames.");

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
            throw new InvalidDataException("The icon does not contain a valid image frame.");

        largestFrame.Freeze();
        return largestFrame;
    }

    private static void RefreshShell(string folderPath)
    {
        SHChangeNotify(ShcneUpdateItem, ShcnfPathW, folderPath, null);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(
        uint eventId,
        uint flags,
        string? item1,
        string? item2);
}