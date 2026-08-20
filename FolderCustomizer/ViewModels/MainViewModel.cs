using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderCustomizer.Services;
using System;
using System.IO;
using System.Windows.Media;

namespace FolderCustomizer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FolderPickerService _folderPickerService;
    private readonly ColorPickerService _colorPickerService;

    private readonly FolderIconService _folderIconService;

    public MainViewModel(FolderPickerService folderPickerService, ColorPickerService colorPickerService, FolderIconService folderIconService)
    {
        _folderPickerService = folderPickerService;
        _colorPickerService = colorPickerService;
        _folderIconService = folderIconService;
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

    #region Commands
    [RelayCommand]
    private void SelectFolder()
    {
        string? folderPath = _folderPickerService.SelectFolder();

        if (string.IsNullOrWhiteSpace(folderPath))
            return;

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
    #endregion

    private static bool HasExistingFolderStyle(string folderPath)
    {
        string iconPath =
            Path.Combine(
                folderPath,
                "custom_icon.ico");

        string desktopIniPath =
            Path.Combine(
                folderPath,
                "desktop.ini");

        if (!File.Exists(iconPath) ||
            !File.Exists(desktopIniPath))
        {
            return false;
        }

        try
        {
            string desktopIni =
                File.ReadAllText(
                    desktopIniPath);

            return desktopIni.Contains(
                "custom_icon.ico",
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}