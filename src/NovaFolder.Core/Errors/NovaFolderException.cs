using System;

namespace NovaFolder.Core.Errors
{
    // Base de los errores "esperables" de NovaFolder: los que tienen un
    // mensaje pensado para mostrarse tal cual al usuario. La interfaz los
    // atrapa y los enseña; cualquier otra excepción es un fallo de
    // programación y va al registro con el manejador global.
    public class NovaFolderException : Exception
    {
        public NovaFolderException() { }
        public NovaFolderException(string mensaje) : base(mensaje) { }
        public NovaFolderException(string mensaje, Exception interna) : base(mensaje, interna) { }
    }

    // Un dato que puso el usuario no es válido (nombre vacío, repetido,
    // ruta inexistente, límite superado...).
    public sealed class ValidacionException : NovaFolderException
    {
        public ValidacionException() { }
        public ValidacionException(string mensaje) : base(mensaje) { }
        public ValidacionException(string mensaje, Exception interna) : base(mensaje, interna) { }
    }

    // Se pidió operar sobre una carpeta que ya no existe: la borraron en
    // otra ventana o se recargó folders.json mientras tanto.
    public sealed class CarpetaNoEncontradaException : NovaFolderException
    {
        public CarpetaNoEncontradaException() { }
        public CarpetaNoEncontradaException(string nombre) : base($"La carpeta «{nombre}» ya no existe.") { }
        public CarpetaNoEncontradaException(string mensaje, Exception interna) : base(mensaje, interna) { }
    }

    // No se pudo leer o escribir la configuración en disco.
    public sealed class ConfiguracionException : NovaFolderException
    {
        public ConfiguracionException() { }
        public ConfiguracionException(string mensaje) : base(mensaje) { }
        public ConfiguracionException(string mensaje, Exception interna) : base(mensaje, interna) { }
    }
}
