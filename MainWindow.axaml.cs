using System;
using System.Collections.Generic;
using Avalonia.Controls;
using android_folder_win11.Controls;
using android_folder_win11.Services;

namespace android_folder_win11
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            AplicarFondoDelSistema();

            var folders = FolderConfigService.Cargar();
            foreach (var folder in folders)
            {
                var control = new FolderControl();
                control.SetFolder(folder);
                FoldersPanel.Children.Add(control);
            }
        }

        // Mica es el material de Windows 11: tiñe la ventana con el fondo de
        // escritorio en vez de desenfocar lo que haya debajo, así que no
        // "arrastra" las ventanas de atrás al moverla. Avalonia 12 lo expone
        // directamente, sin P/Invoke a DwmSetWindowAttribute.
        //
        // La lista es por orden de preferencia: el sistema coge la primera que
        // pueda dar. En Linux normalmente cae a Transparent o a ninguna, por eso
        // existe FondoRespaldo.
        private void AplicarFondoDelSistema()
        {
            TransparencyLevelHint = new List<WindowTransparencyLevel>
            {
                WindowTransparencyLevel.Mica,
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Blur
            };

            // ActualTransparencyLevel solo es fiable cuando la ventana ya existe.
            Opened += (_, _) =>
            {
                var nivel = ActualTransparencyLevel;
                var hayMaterial = nivel == WindowTransparencyLevel.Mica
                               || nivel == WindowTransparencyLevel.AcrylicBlur
                               || nivel == WindowTransparencyLevel.Blur;

                FondoRespaldo.IsVisible = !hayMaterial;
                // El nivel aplicado va al log, no al encabezado: sirve para
                // diagnosticar en Windows sin ensuciar la interfaz.
                Console.Error.WriteLine($"Fondo aplicado por el sistema: {nivel}");
            };
        }
    }
}
