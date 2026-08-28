using Avalonia.Controls;
using android_folder_win11.Controls;
using android_folder_win11.Services;

namespace android_folder_win11 // <-- cambia esto si tu proyecto usa otro namespace
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var folders = FolderConfigService.Cargar();
            foreach (var folder in folders)
            {
                var control = new FolderControl();
                control.SetFolder(folder);
                FoldersPanel.Children.Add(control);
            }
        }
    }
}
