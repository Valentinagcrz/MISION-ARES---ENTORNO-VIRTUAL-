using System.Diagnostics;

namespace TeleoperacionRV
{
    /// <summary>Una línea de texto recibida del control, con la hora exacta de llegada.</summary>
    public struct LineaRecibida
    {
        public string texto;
        public double tiempoMs;
    }

    /// <summary>
    /// Canal de comunicación con el control háptico. Hay una implementación por
    /// plataforma: Bluetooth en Android y puerto serie (COM) en Windows.
    /// Todas leen en un hilo aparte para no congelar Unity.
    /// </summary>
    public interface ITransporte
    {
        string Descripcion { get; }
        bool Conectado { get; }
        void Iniciar();
        void Detener();
        bool IntentarLeer(out LineaRecibida linea);
        void Enviar(string linea);
    }

    /// <summary>Reloj de alta resolución compartido (sirve para medir latencia).</summary>
    public static class Reloj
    {
        static readonly Stopwatch sw = Stopwatch.StartNew();
        public static double Ms => sw.Elapsed.TotalMilliseconds;
    }
}
