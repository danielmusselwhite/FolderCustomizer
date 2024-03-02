using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;

namespace FolderCustomizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Btn_Load_Click(object sender, RoutedEventArgs e)
        {
            // Popup to let user select FOLDER/DIRECTORY to customize
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = "Select a folder to customize";
            folderBrowserDialog.ShowDialog();
            updateSelectedFolderTxt(folderBrowserDialog.SelectedPath);




        }

        private void updateSelectedFolderTxt(string foldername)
        {
            // Update the textbox with the selected folder or folder
            txt_SelectedFolder.Text = foldername;
        }
    }
}
