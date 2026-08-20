using System.Windows.Controls;

namespace FolderCustomizer.Views.Controls;

/// <summary>
/// Represents the properties panel displayed alongside the folder icon editor.
/// </summary>
/// <remarks>
/// The control provides the user interface for viewing and editing folder
/// customisation settings. Its visual structure and bindings are defined in XAML,
/// while the underlying state and commands are supplied by the inherited data context,
/// typically a <see cref="ViewModels.MainViewModel"/>.
/// </remarks>
public partial class FolderPropertiesView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FolderPropertiesView"/> class.
    /// </summary>
    public FolderPropertiesView()
    {
        InitializeComponent();
    }
}