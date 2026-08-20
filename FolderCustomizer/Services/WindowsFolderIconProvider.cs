using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace FolderCustomizer.Services;

public static class WindowsFolderIconProvider
{
    // SHSTOCKICONID
    private const uint SiidFolder = 3;

    // SHGetStockIconInfo flags
    private const uint ShgsiSysIconIndex = 0x000004000;

    // SHGetImageList sizes
    private const int ShilJumbo = 0x4;

    // ImageList_GetIcon flags
    private const uint IldTransparent = 0x00000001;

    /// <summary>
    /// Gets the current Windows default folder icon at the
    /// highest standard Shell resolution (normally 256x256).
    /// </summary>
    public static BitmapSource GetDefaultFolderIcon()
    {
        int iconIndex = GetFolderIconIndex();

        IntPtr imageList =
            GetJumboImageList();

        IntPtr iconHandle =
            ImageList_GetIcon(
                imageList,
                iconIndex,
                IldTransparent);

        if (iconHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Windows returned an invalid folder icon handle.");
        }

        try
        {
            BitmapSource bitmap =
                Imaging.CreateBitmapSourceFromHIcon(
                    iconHandle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

            bitmap.Freeze();

            return bitmap;
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private static int GetFolderIconIndex()
    {
        var info = new SHSTOCKICONINFO
        {
            cbSize =
                (uint)Marshal.SizeOf<SHSTOCKICONINFO>()
        };

        int result =
            SHGetStockIconInfo(
                SiidFolder,
                ShgsiSysIconIndex,
                ref info);

        if (result != 0)
        {
            throw new InvalidOperationException(
                $"Could not retrieve the Windows folder icon index. " +
                $"HRESULT: 0x{result:X8}");
        }

        return info.iSysImageIndex;
    }

    private static IntPtr GetJumboImageList()
    {
        Guid imageListGuid =
            new("46EB5926-582E-4017-9FDF-E8998DAA0950");

        int result =
            SHGetImageList(
                ShilJumbo,
                ref imageListGuid,
                out IntPtr imageList);

        if (result != 0 ||
            imageList == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Could not retrieve the Windows jumbo image list. " +
                $"HRESULT: 0x{result:X8}");
        }

        return imageList;
    }

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int SHGetStockIconInfo(
        uint siid,
        uint flags,
        ref SHSTOCKICONINFO info);

    [DllImport(
        "shell32.dll",
        EntryPoint = "#727")]
    private static extern int SHGetImageList(
        int imageList,
        ref Guid riid,
        out IntPtr ppv);

    [DllImport(
        "comctl32.dll",
        SetLastError = true)]
    private static extern IntPtr ImageList_GetIcon(
        IntPtr imageList,
        int index,
        uint flags);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool DestroyIcon(
        IntPtr icon);

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct SHSTOCKICONINFO
    {
        public uint cbSize;

        public IntPtr hIcon;

        public int iSysImageIndex;

        public int iIcon;

        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 260)]
        public string szPath;
    }
}