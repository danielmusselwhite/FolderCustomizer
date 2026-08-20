using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderCustomizer.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace FolderCustomizer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public event Func<string, Task>? RenderRequested;

    private readonly FolderPickerService _folderPickerService;
    private readonly ColorPickerService _colorPickerService;
    private readonly FolderIconService _folderIconService;
    private readonly ImageProcessingService _imageProcessingService;
    private readonly ImagePickerService _imagePickerService;

    public MainViewModel(FolderPickerService folderPickerService, ColorPickerService colorPickerService, FolderIconService folderIconService, ImageProcessingService imageProcessingService, ImagePickerService imagePickerService)
    {
        _folderPickerService = folderPickerService;
        _colorPickerService = colorPickerService;
        _folderIconService = folderIconService;
        _imageProcessingService = imageProcessingService;
        _imagePickerService = imagePickerService;
    }

    [ObservableProperty]
    private string? selectedFolderPath;

    [ObservableProperty]
    private bool isEditorEnabled;

    [ObservableProperty]
    private bool hasExistingStyle;

    [ObservableProperty]
    private string selectedColourText = "Default";

    [ObservableProperty]
    private Color? selectedColour;

    public ObservableCollection<EditorImageViewModel> OverlayImages { get; } = [];

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
    }

    [RelayCommand]
    private void ChooseColour()
    {
        Color? colour = _colorPickerService.PickColor(selectedColour);

        if (colour is null)
            return;

        SelectedColour = colour;

        SelectedColourText =
            $"#{colour.Value.R:X2}" +
            $"{colour.Value.G:X2}" +
            $"{colour.Value.B:X2}";
    }

    [RelayCommand]
    private void ResetColour()
    {
        SelectedColour = null;

        SelectedColourText = "Default";
    }

    [RelayCommand]
    private void ClearStyle()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath))
            return;

        _folderIconService.ClearCustomStyle(SelectedFolderPath);
        OverlayImages.Clear();

        SelectedFolderPath = null;
        SelectedColour = null;
        SelectedColourText = "Default";
        IsEditorEnabled = false;
        HasExistingStyle = false;
    }

    [RelayCommand]
    private void AddImage()
    {
        string? imagePath = _imagePickerService.SelectImage();

        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        OverlayImages.Add(new EditorImageViewModel(imagePath));
    }

    [RelayCommand]
    private async Task ApplyIconAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath))
            return;

        string folderPath = SelectedFolderPath;
        string pngPath = Path.Combine(folderPath, "custom_icon.png");
        string icoPath = Path.Combine(folderPath, "custom_icon.ico");

        if (RenderRequested is null)
            return;

        await RenderRequested.Invoke(pngPath);

        if (File.Exists(icoPath))
        {
            File.SetAttributes(icoPath, FileAttributes.Normal);
            File.Delete(icoPath);
        }

        ImagingHelper.ConvertToIcon(pngPath, icoPath, 256);

        if (File.Exists(pngPath))
            File.Delete(pngPath);

        _folderIconService.ApplyCustomIcon(folderPath, icoPath);

        OverlayImages.Clear();

        SelectedFolderPath = null;
        SelectedColour = null;
        SelectedColourText = "Default";
        IsEditorEnabled = false;
        HasExistingStyle = false;
    }
    #endregion

}