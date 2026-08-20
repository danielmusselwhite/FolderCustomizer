using FolderCustomizer.Services;
using FolderCustomizer.ViewModels;
using System.Windows;

namespace FolderCustomizer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainViewModel(
            new FolderPickerService(),
            new ColorPickerService(),
            new FolderIconService(),
            new ImageProcessingService(),
            new ImagePickerService());
    }
}