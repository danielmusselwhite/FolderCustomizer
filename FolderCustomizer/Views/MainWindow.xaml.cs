using FolderCustomizer.Services;
using FolderCustomizer.ViewModels;
using System.Windows;

namespace FolderCustomizer.Views;

/// <summary>
/// Represents the application's primary window and composes the main FolderCustomizer UI.
/// </summary>
/// <remarks>
/// <para>
/// The window acts as the application's composition root. It creates the concrete services
/// required by <see cref="MainViewModel"/> and assigns the configured view model to the
/// window's <see cref="FrameworkElement.DataContext"/>.
/// </para>
///
/// <para>
/// The visual layout itself is defined in XAML and is divided into three primary areas:
/// an application command bar, the main editing workspace, and a status area. The editing
/// workspace is further composed from dedicated controls such as the folder editor and
/// folder properties views.
/// </para>
///
/// <para>
/// Application state and command handling are intentionally kept out of the window
/// code-behind and delegated to <see cref="MainViewModel"/> and its supporting services.
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes the main application window and constructs its view model dependencies.
    /// </summary>
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