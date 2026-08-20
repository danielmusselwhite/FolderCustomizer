using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderCustomizer.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderCustomizer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FolderPickerService _folderPickerService;
    private readonly ColorPickerService _colorPickerService;
    private readonly FolderIconService _folderIconService;
    private readonly ImageProcessingService _imageProcessingService;
    private readonly ImagePickerService _imagePickerService;

    public MainViewModel(
        FolderPickerService folderPickerService,
        ColorPickerService colorPickerService,
        FolderIconService folderIconService,
        ImageProcessingService imageProcessingService,
        ImagePickerService imagePickerService)
    {
        _folderPickerService = folderPickerService;
        _colorPickerService = colorPickerService;
        _folderIconService = folderIconService;
        _imageProcessingService = imageProcessingService;
        _imagePickerService = imagePickerService;

        RefreshFolderPreview();
    }

    #region Events

    public event Func<string, Task>? RenderRequested;

    #endregion

    #region Properties

    [ObservableProperty]
    private string? selectedFolderPath;

    [ObservableProperty]
    private bool isEditorEnabled;

    [ObservableProperty]
    private bool hasExistingStyle;

    [ObservableProperty]
    private Color? selectedColour;

    [ObservableProperty]
    private string selectedColourText = "Default";

    [ObservableProperty]
    private BitmapSource? folderPreview;

    [ObservableProperty]
    private Brush colourPreviewBrush = Brushes.Transparent;

    [ObservableProperty]
    private Brush colourPreviewBorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));

    public ObservableCollection<EditorImageViewModel> OverlayImages { get; } = [];

    #endregion

    #region Commands

    [RelayCommand]
    private void SelectFolder()
    {
        string? folderPath = _folderPickerService.SelectFolder();

        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        OverlayImages.Clear();

        SelectedFolderPath = folderPath;
        SelectedColour = null;
        SelectedColourText = "Default";
        IsEditorEnabled = true;
        HasExistingStyle = _folderIconService.HasCustomStyle(folderPath);

        RefreshColourPreview();
        RefreshFolderPreview();
    }

    [RelayCommand]
    private void ChooseColour()
    {
        Color? colour = _colorPickerService.PickColor(SelectedColour);

        if (colour is null)
            return;

        SelectedColour = colour;
        SelectedColourText = $"#{colour.Value.R:X2}{colour.Value.G:X2}{colour.Value.B:X2}";

        RefreshColourPreview();
        RefreshFolderPreview();
    }

    [RelayCommand]
    private void ResetColour()
    {
        SelectedColour = null;
        SelectedColourText = "Default";

        RefreshColourPreview();
        RefreshFolderPreview();
    }

    [RelayCommand]
    private void ClearStyle()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath))
            return;

        _folderIconService.ClearCustomStyle(SelectedFolderPath);

        ResetEditor();
    }

    [RelayCommand]
    private void AddImage()
    {
        string? imagePath = _imagePickerService.SelectImage();

        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        var image = new EditorImageViewModel(imagePath)
        {
            SelectAction = SelectOverlay,
            DeleteAction = RemoveOverlay
        };

        OverlayImages.Add(image);

        SelectOverlay(image);
    }

    [RelayCommand]
    private async Task ApplyIconAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath))
            return;

        if (RenderRequested is null)
            return;

        string folderPath = SelectedFolderPath;
        string pngPath = Path.Combine(folderPath, "custom_icon.png");
        string icoPath = Path.Combine(folderPath, "custom_icon.ico");

        try
        {
            await RenderRequested.Invoke(pngPath);

            if (File.Exists(icoPath))
            {
                File.SetAttributes(icoPath, FileAttributes.Normal);
                File.Delete(icoPath);
            }

            ImagingHelper.ConvertToIcon(pngPath, icoPath, 256);

            _folderIconService.ApplyCustomIcon(folderPath, icoPath);

            ResetEditor();
        }
        finally
        {
            if (File.Exists(pngPath))
                File.Delete(pngPath);
        }
    }

    #endregion

    #region Overlay Management

    public void SelectOverlay(EditorImageViewModel selectedImage)
    {
        foreach (EditorImageViewModel image in OverlayImages)
            image.IsSelected = ReferenceEquals(image, selectedImage);
    }

    private void RemoveOverlay(EditorImageViewModel image)
    {
        OverlayImages.Remove(image);
    }

    #endregion

    #region Preview

    private void RefreshFolderPreview()
    {
        BitmapSource source = !string.IsNullOrWhiteSpace(SelectedFolderPath)
            ? _folderIconService.GetFolderIcon(SelectedFolderPath)
            : WindowsFolderIconProvider.GetDefaultFolderIcon();

        if (SelectedColour is Color colour)
            source = _imageProcessingService.ApplyColor(source, colour);

        FolderPreview = source;
    }

    private void RefreshColourPreview()
    {
        if (SelectedColour is not Color colour)
        {
            ColourPreviewBrush = Brushes.Transparent;
            ColourPreviewBorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));
            return;
        }

        ColourPreviewBrush = new SolidColorBrush(colour);
        ColourPreviewBorderBrush = new SolidColorBrush(GetPreviewBorderColor(colour));
    }

    private static Color GetPreviewBorderColor(Color color)
    {
        const double darkenFactor = 0.78;

        return Color.FromArgb(
            color.A,
            (byte)(color.R * darkenFactor),
            (byte)(color.G * darkenFactor),
            (byte)(color.B * darkenFactor));
    }

    #endregion

    #region State

    private void ResetEditor()
    {
        OverlayImages.Clear();

        SelectedFolderPath = null;
        SelectedColour = null;
        SelectedColourText = "Default";
        IsEditorEnabled = false;
        HasExistingStyle = false;

        RefreshColourPreview();
        RefreshFolderPreview();
    }

    #endregion
}