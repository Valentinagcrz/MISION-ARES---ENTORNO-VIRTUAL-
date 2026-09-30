using System;
using System.Collections.Concurrent;
using System.Threading;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.IO.Ports;
#endif

namespace TeleoperacionRV
{
    /// <summary>
    /// Comunicación con el ESP32 por puerto COM en Windows. Sirve para:
    ///  - Bluetooth: al emparejar el ESP32 en Windows aparecen puertos COM "Standard Serial over Bluetooth".
    ///  - Cable USB: el ESP32 también envía los datos por USB.
    /// Con puerto "AUTO" prueba todos los puertos hasta encontrar uno que envíe tramas del control.
    /// </summary>
    public class TransporteSerialPC : ITransporte
    {
        readonly string puertoPedido;
        readonly int baudios;
        readonly ConcurrentQueue<LineaRecibida> cola = new ConcurrentQueue<LineaRecibida>();
        Thread hilo;
        volatile bool activo;
        volatile bool conectado;
        volatile string estado = "Serial: iniciando";
        double tUltimaRx;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        SerialPort puerto;
        readonly object candado = new object();
#endif

        public TransporteSerialPC(string puerto, int baudios)
        {
            puertoPedido = string.IsNullOrWhiteSpace(puerto) ? "AUTO" : puerto.Trim().ToUpperInvariant();
            this.baudios = baudios;
        }

        public string Descripcion => estado;
        public bool Conectado => conectado;

        public void Iniciar()
        {
            activo = true;
            hilo = new Thread(Bucle) { IsBackground = true, Name = "TransporteSerialPC" };
            hilo.Start();
        }

        public void Detener()
        {
            activo = false;
            Cerrar();
            if (hilo != null && hilo.IsAlive) hilo.Join(500);
        }

        public bool IntentarLeer(out LineaRecibida linea) => cola.TryDequeue(out linea);

        public void Enviar(string linea)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (!conectado) return;
            try
            {
                lock (candado) { puerto?.Write(linea + "\n"); }
            }
            catch (Exception) { /* se reconectará en el hilo de lectura */ }
#endif
        }

        void Bucle()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            while (activo)
            {
                if (!conectado)
                {
                    Conectar();
                    if (!conectado) { Dormir(2000); continue; }
                }
                try
                {
                    string l = puerto.ReadLine();
                    tUltimaRx = Reloj.Ms;
                    l = l.Trim();
                    if (l.Length > 0) cola.Enqueue(new LineaRecibida { texto = l, tiempoMs = tUltimaRx });
                }
                catch (TimeoutException)
                {
                    if (Reloj.Ms - tUltimaRx > 3000) { estado = "Serial: sin datos, reconectando"; Cerrar(); }
                }
                catch (Exception e)
                {
                    estado = "Serial: error (" + e.Message + ")";
                    Cerrar();
                }
            }
#else
            estado = "Serial: no disponible en esta plataforma";
#endif
        }

        void Dormir(int ms)
        {
            for (int t = 0; t < ms && activo; t += 100) Thread.Sleep(100);
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        void Conectar()
        {
            string[] candidatos;
            if (puertoPedido == "AUTO")
            {
                try { candidatos = SerialPort.GetPortNames(); }
                catch (Exception) { candidatos = new string[0]; }
                if (candidatos.Length == 0) { estado = "Serial: no hay puertos COM (¿emparejaste el ESP32?)"; return; }
            }
            else candidatos = new[] { puertoPedido };

            foreach (var nombre in candidatos)
            {
                if (!activo) return;
                estado = "Serial: probando " + nombre;
                SerialPort sp = null;
                try
                {
                    sp = new SerialPort(nombre, baudios) { ReadTimeout = 300, WriteTimeout = 300, NewLine = "\n", DtrEnable = false, RtsEnable = false };
                    sp.Open();
                    // Espera una trama válida del control (hasta 2.5 s)
                    double limite = Reloj.Ms + 2500;
                    bool valido = false;
                    while (Reloj.Ms < limite && activo)
                    {
                        try
                        {
                            string l = sp.ReadLine().Trim();
                            if (l.StartsWith("D,") || l.StartsWith("I,")) { valido = true; break; }
                        }
                        catch (TimeoutException) { }
                    }
                    if (valido)
                    {
                        lock (candado) { puerto = sp; }
                        tUltimaRx = Reloj.Ms;
                        conectado = true;
                        estado = "Serial " + nombre;
                        return;
                    }
                    sp.Close();
                }
                catch (Exception)
                {
                    try { sp?.Close(); } catch (Exception) { }
                }
            }
            estado = puertoPedido == "AUTO" ? "Serial: control no encontrado (reintentando)" : "Serial: no se pudo abrir " + puertoPedido;
        }

        void Cerrar()
        {
            conectado = false;
            lock (candado)
            {
                try { if (puerto != null && puerto.IsOpen) puerto.Close(); } catch (Exception) { }
                puerto = null;
            }
        }
#else
        void Conectar() { }
        void Cerrar() { conectado = false; }
#endif
    }
}
