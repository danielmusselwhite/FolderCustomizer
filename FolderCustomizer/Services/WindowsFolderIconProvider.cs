using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace FolderCustomizer.Services;

/// <summary>
/// Provides access to the current Windows default folder icon.
/// </summary>
/// <remarks>
/// The provider retrieves the system folder icon from the Windows Shell rather than
/// relying on an application-bundled image. This allows FolderCustomizer to display
/// the folder artwork associated with the user's current Windows installation.
///
/// The implementation uses native Shell and image-list APIs to locate the system folder
/// icon and request its jumbo-resolution representation, which is normally 256x256 pixels.
/// </remarks>
public static class WindowsFolderIconProvider
{
    #region Windows Shell Constants

    /// <summary>
    /// Identifies the standard folder icon in the Windows Shell stock icon collection.
    /// </summary>
    private const uint SiidFolder = 3;

    /// <summary>
    /// Requests the system image-list index for a stock Shell icon.
    /// </summary>
    private const uint ShgsiSysIconIndex = 0x000004000;

    /// <summary>
    /// Identifies the jumbo Windows Shell image list, normally containing
    /// 256x256 pixel icons.
    /// </summary>
    private const int ShilJumbo = 0x4;

    /// <summary>
    /// Requests that an icon retrieved from an image list preserve transparency.
    /// </summary>
    private const uint IldTransparent = 0x00000001;

    #endregion

    #region Public API

    /// <summary>
    /// Gets the current Windows default folder icon at the highest standard
    /// Shell resolution, normally 256x256 pixels.
    /// </summary>
    /// <returns>
    /// A frozen <see cref="BitmapSource"/> containing the Windows folder icon.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Windows cannot provide the folder icon index, jumbo image list,
    /// or a valid native icon handle.
    /// </exception>
    /// <remarks>
    /// The native icon handle returned by Windows is converted into a WPF
    /// <see cref="BitmapSource"/> and released immediately afterwards.
    ///
    /// The resulting bitmap is frozen so that it becomes immutable and can be safely
    /// reused by WPF where appropriate.
    /// </remarks>
    public static BitmapSource GetDefaultFolderIcon()
    {
        int iconIndex = GetFolderIconIndex();
        IntPtr imageList = GetJumboImageList();

        IntPtr iconHandle = ImageList_GetIcon(
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
            BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmap.Freeze();

            return bitmap;
        }
        finally
        {
            // ImageList_GetIcon creates a native HICON that the caller owns,
            // so it must always be released after the WPF bitmap has been created.
            DestroyIcon(iconHandle);
        }
    }

    #endregion

    #region Shell Helpers

    /// <summary>
    /// Retrieves the system image-list index associated with the Windows folder icon.
    /// </summary>
    /// <returns>
    /// The index of the folder icon within the Windows system image list.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Windows Shell cannot provide stock icon information.
    /// </exception>
    private static int GetFolderIconIndex()
    {
        var info = new SHSTOCKICONINFO
        {
            cbSize = (uint)Marshal.SizeOf<SHSTOCKICONINFO>()
        };

        int result = SHGetStockIconInfo(
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

    /// <summary>
    /// Retrieves the Windows Shell jumbo image list.
    /// </summary>
    /// <returns>
    /// A native pointer to the Shell image list containing jumbo-sized icons.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Windows cannot provide a valid jumbo image-list pointer.
    /// </exception>
    /// <remarks>
    /// The image list is requested through the Shell image-list interface identified
    /// by IID <c>46EB5926-582E-4017-9FDF-E8998DAA0950</c>.
    /// </remarks>
    private static IntPtr GetJumboImageList()
    {
        Guid imageListGuid =
            new("46EB5926-582E-4017-9FDF-E8998DAA0950");

        int result = SHGetImageList(
            ShilJumbo,
            ref imageListGuid,
            out IntPtr imageList);

        if (result != 0 || imageList == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Could not retrieve the Windows jumbo image list. " +
                $"HRESULT: 0x{result:X8}");
        }

        return imageList;
    }

    #endregion

    #region Native Methods

    /// <summary>
    /// Retrieves information about a stock icon provided by the Windows Shell.
    /// </summary>
    /// <param name="siid">
    /// The identifier of the requested stock icon.
    /// </param>
    /// <param name="flags">
    /// Flags controlling which icon information should be returned.
    /// </param>
    /// <param name="info">
    /// Receives information about the requested stock icon.
    /// </param>
    /// <returns>
    /// An HRESULT where zero indicates success.
    /// </returns>
    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int SHGetStockIconInfo(
        uint siid,
        uint flags,
        ref SHSTOCKICONINFO info);

    /// <summary>
    /// Retrieves one of the Windows Shell system image lists.
    /// </summary>
    /// <param name="imageList">
    /// Identifies the requested image-list size.
    /// </param>
    /// <param name="riid">
    /// The interface identifier used to request the image list.
    /// </param>
    /// <param name="ppv">
    /// Receives a pointer to the requested image list.
    /// </param>
    /// <returns>
    /// An HRESULT where zero indicates success.
    /// </returns>
    /// <remarks>
    /// <c>SHGetImageList</c> is imported using shell32 ordinal 727 for compatibility
    /// with the native Windows Shell export.
    /// </remarks>
    [DllImport(
        "shell32.dll",
        EntryPoint = "#727")]
    private static extern int SHGetImageList(
        int imageList,
        ref Guid riid,
        out IntPtr ppv);

    /// <summary>
    /// Creates an icon from an image stored in a native Windows image list.
    /// </summary>
    /// <param name="imageList">
    /// A pointer to the source image list.
    /// </param>
    /// <param name="index">
    /// The index of the image to retrieve.
    /// </param>
    /// <param name="flags">
    /// Flags controlling how the icon should be rendered.
    /// </param>
    /// <returns>
    /// A native icon handle, or <see cref="IntPtr.Zero"/> if retrieval fails.
    /// </returns>
    [DllImport(
        "comctl32.dll",
        SetLastError = true)]
    private static extern IntPtr ImageList_GetIcon(
        IntPtr imageList,
        int index,
        uint flags);

    /// <summary>
    /// Releases a native icon handle created by Windows.
    /// </summary>
    /// <param name="icon">
    /// The native icon handle to release.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the icon was successfully destroyed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool DestroyIcon(
        IntPtr icon);

    #endregion

    #region Native Structures

    /// <summary>
    /// Contains information describing a stock icon provided by the Windows Shell.
    /// </summary>
    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct SHSTOCKICONINFO
    {
        /// <summary>
        /// Gets or sets the size, in bytes, of this structure.
        /// </summary>
        public uint cbSize;

        /// <summary>
        /// Gets or sets the native icon handle returned by the Shell.
        /// </summary>
        public IntPtr hIcon;

        /// <summary>
        /// Gets or sets the icon's index within the system image list.
        /// </summary>
        public int iSysImageIndex;

        /// <summary>
        /// Gets or sets the icon's resource identifier.
        /// </summary>
        public int iIcon;

        /// <summary>
        /// Gets or sets the path of the file containing the icon resource.
        /// </summary>
        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 260)]
        public string szPath;
    }

    #endregion
}