using System;
using NovaFolder.Core;
using NovaFolder.Core.Storage;
using NovaFolder.Services.Updates;

namespace NovaFolder.Views.Principal
{
    // Lo que la ventana principal y sus páginas necesitan del resto de la
    // app. Lo arma App (la raíz de composición) y lo pasa entero: así las
    // páginas no crean servicios ni conocen otras ventanas.
    public sealed record ServiciosApp(
        FolderStore Store,
        RutasApp Rutas,
        UpdateService Actualizaciones,
        Action MostrarBienvenida,
        Func<bool> WidgetVisible,
        Action<bool> MostrarWidget,
        Action<bool> Autoinicio,
        Action<bool> LimpiarEscritorio,
        Action GuardarAccesosDelEscritorio,
        Action Salir);

    // Barra de avisos de la ventana principal ("Se organizaron 12
    // elementos · Deshacer"). Las páginas avisan a través de esto.
    public interface IAvisador
    {
        void Avisar(string texto, bool esError = false, string? textoAccion = null, Action? accion = null);
    }
}
