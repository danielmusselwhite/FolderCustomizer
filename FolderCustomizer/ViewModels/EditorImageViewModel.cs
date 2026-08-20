using CommunityToolkit.Mvvm.ComponentModel;

namespace FolderCustomizer.ViewModels;

public partial class EditorImageViewModel : ObservableObject
{
    public EditorImageViewModel(string imagePath)
    {
        ImagePath = imagePath;
    }

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
}