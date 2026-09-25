using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NovaFolder.Controls;
using NovaFolder.Views.Principal;

namespace NovaFolder.Views.Paginas
{
    // Ayuda siempre a mano: los gestos principales en tarjetas y las dudas
    // habituales. La guía paso a paso se puede volver a abrir desde aquí.
    public partial class AyudaPagina : UserControl
    {
        private static readonly (string Glifo, string Titulo, string Texto)[] ListaConsejos =
        {
            ("\uE8F4", "Crear carpetas", "En Carpetas, pulsa «Nueva carpeta» y escribe su nombre. También puedes dejar que «Ordenar Escritorio» las cree por ti."),
            ("\uE7C2", "Llenarlas", "Arrastra accesos directos, juegos o archivos desde el Escritorio o el Explorador y suéltalos sobre una carpeta."),
            ("\uEA99", "Ordenar el Escritorio", "«Ordenar Escritorio» revisa lo que está suelto, sugiere una carpeta para cada cosa y lo guarda todo de un clic."),
            ("\uE8A7", "Abrir", "Clic en un elemento para abrirlo. Desde el Escritorio, doble clic en el ícono de una carpeta para verla sin abrir NovaFolder."),
            ("\uE8CB", "Reordenar y mover", "Arrastra dentro de una carpeta para cambiar el orden, o sobre otra carpeta de la lista para moverlo."),
            ("\uE8A0", "Sacar algo", "Arrástralo al Escritorio, o clic derecho → «Devolver al Escritorio». Nada se borra nunca."),
            ("\uE8AC", "Renombrar y eliminar", "Botones junto al nombre de la carpeta, o clic derecho sobre ella en la lista. Eliminar se puede deshacer."),
            ("\uE7E8", "Cerrar la ventana", "NovaFolder sigue junto al reloj para que las carpetas del Escritorio funcionen. Un clic en su ícono vuelve a abrir esta ventana.")
        };

        private static readonly (string Pregunta, string Respuesta)[] ListaPreguntas =
        {
            ("¿Se borran mis archivos?",
             "No. NovaFolder solo guarda dónde está cada cosa. Con «Limpiar el Escritorio», los accesos directos se mueven a la carpeta de datos de NovaFolder y vuelven al Escritorio en cuanto los quitas de una carpeta o desinstalas la app."),
            ("¿Por qué algunos accesos siguen en el Escritorio?",
             "Los del Escritorio común (los que un instalador puso para todos los usuarios) necesitan permisos de administrador para moverse, así que se organizan pero Windows los sigue mostrando."),
            ("¿Dónde está NovaFolder cuando cierro la ventana?",
             "En la bandeja del sistema, junto al reloj (si no lo ves, pulsa la flecha ^). Para cerrarlo del todo: Ajustes → «Salir de NovaFolder»."),
            ("¿Cómo lo desinstalo?",
             "Configuración de Windows → Aplicaciones → NovaFolder → Desinstalar. Tus accesos directos vuelven al Escritorio y no queda nada en el sistema.")
        };

        public AyudaPagina() => InitializeComponent();

        public AyudaPagina(ServiciosApp servicios)
        {
            InitializeComponent();
            BotonGuia.Click += (_, _) => servicios.MostrarBienvenida();

            foreach (var (glifo, titulo, texto) in ListaConsejos)
                Consejos.Children.Add(Tarjeta(glifo, titulo, texto));

            foreach (var (pregunta, respuesta) in ListaPreguntas)
            {
                Preguntas.Children.Add(new Border
                {
                    Classes = { "tarjeta" },
                    Child = new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            new TextBlock { Text = pregunta, Classes = { "texto" }, FontWeight = FontWeight.SemiBold },
                            new TextBlock { Text = respuesta, Classes = { "suave" }, FontSize = 13, TextWrapping = TextWrapping.Wrap }
                        }
                    }
                });
            }
        }

        private static Border Tarjeta(string glifo, string titulo, string texto) => new()
        {
            Classes = { "tarjeta" },
            Margin = new Thickness(0, 0, 12, 12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Children =
                {
                    new Border
                    {
                        Width = 40, Height = 40,
                        CornerRadius = new CornerRadius(10),
                        Background = (IBrush?)Application.Current?.FindResource("NovaAcentoSuave"),
                        VerticalAlignment = VerticalAlignment.Top,
                        Child = new TextBlock
                        {
                            Text = glifo,
                            FontFamily = Interacciones.FuenteIconos,
                            FontSize = 18,
                            Foreground = Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    EnColumna(new StackPanel
                    {
                        Margin = new Thickness(14, 0, 0, 0),
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock { Text = titulo, Classes = { "texto" }, FontWeight = FontWeight.SemiBold },
                            new TextBlock { Text = texto, Classes = { "suave" }, FontSize = 12.5, TextWrapping = TextWrapping.Wrap }
                        }
                    })
                }
            }
        };

        private static Control EnColumna(Control c)
        {
            Grid.SetColumn(c, 1);
            return c;
        }
    }
}
