using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace FolderCustomizer.ViewModels;

public partial class EditorImageViewModel : ObservableObject
{
    public EditorImageViewModel(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        ImagePath = imagePath;
    }

    #region Actions

    public Action<EditorImageViewModel>? SelectAction { get; set; }

    public Action<EditorImageViewModel>? DeleteAction { get; set; }

    #endregion

    #region Properties

    [ObservableProperty]
    private string imagePath;

    [ObservableProperty]
    private double x;

    [ObservableProperty]
    private double y;

    [ObservableProperty]
    private double width;

    [ObservableProperty]
    private double height;

    [ObservableProperty]
    private double rotation;

    [ObservableProperty]
    private bool isSelected;

    #endregion
}