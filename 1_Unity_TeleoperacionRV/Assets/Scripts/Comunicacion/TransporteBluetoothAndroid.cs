using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Bluetooth clásico (perfil SPP / RFCOMM) en Android, usando las clases de Java
    /// del sistema a través de JNI. No necesita plugins externos.
    /// Requisito: emparejar primero el ESP32 ("ControlHaptico_RV") en los ajustes
    /// de Bluetooth del celular.
    /// </summary>
    public class TransporteBluetoothAndroid : ITransporte
    {
        const string UUID_SPP = "00001101-0000-1000-8000-00805F9B34FB";

        readonly string nombreDispositivo;
        readonly ConcurrentQueue<LineaRecibida> cola = new ConcurrentQueue<LineaRecibida>();
        Thread hilo;
        volatile bool activo;
        volatile bool conectado;
        volatile string estado = "BT: iniciando";

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject socket, lector, escritor;
        readonly object candado = new object();
#endif

        public TransporteBluetoothAndroid(string nombre)
        {
            nombreDispositivo = nombre;
        }

        public string Descripcion => estado;
        public bool Conectado => conectado;

        public void Iniciar()
        {
            activo = true;
            hilo = new Thread(Bucle) { IsBackground = true, Name = "TransporteBluetooth" };
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
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!conectado) return;
            try
            {
                lock (candado)
                {
                    if (escritor == null) return;
                    escritor.Call("write", linea + "\n");
                    escritor.Call("flush");
                }
            }
            catch (Exception) { /* el hilo de lectura detectará la desconexión */ }
#endif
        }

        void Bucle()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJNI.AttachCurrentThread();
            try
            {
                while (activo)
                {
                    if (!conectado)
                    {
                        Conectar();
                        if (!conectado) { Dormir(3000); continue; }
                    }
                    try
                    {
                        AndroidJavaObject l;
                        lock (candado) { l = lector; }
                        if (l == null) throw new Exception("sin lector");
                        string s = l.Call<string>("readLine");
                        if (s == null) throw new Exception("conexión cerrada");
                        s = s.Trim();
                        if (s.Length > 0) cola.Enqueue(new LineaRecibida { texto = s, tiempoMs = Reloj.Ms });
                    }
                    catch (Exception)
                    {
                        if (activo) estado = "BT: conexión perdida, reintentando";
                        Cerrar();
                    }
                }
            }
            finally
            {
                Cerrar();
                AndroidJNI.DetachCurrentThread();
            }
#else
            estado = "BT: solo disponible en Android";
#endif
        }

        void Dormir(int ms)
        {
            for (int t = 0; t < ms && activo; t += 100) Thread.Sleep(100);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        void Conectar()
        {
            try
            {
                using (var claseAdaptador = new AndroidJavaClass("android.bluetooth.BluetoothAdapter"))
                {
                    var adaptador = claseAdaptador.CallStatic<AndroidJavaObject>("getDefaultAdapter");
                    if (adaptador == null) { estado = "BT: este celular no tiene Bluetooth"; return; }
                    if (!adaptador.Call<bool>("isEnabled")) { estado = "BT: activa el Bluetooth del celular"; return; }

                    AndroidJavaObject dispositivo = null;
                    var emparejados = adaptador.Call<AndroidJavaObject>("getBondedDevices");
                    var it = emparejados.Call<AndroidJavaObject>("iterator");
                    while (it.Call<bool>("hasNext"))
                    {
                        var d = it.Call<AndroidJavaObject>("next");
                        string n = d.Call<string>("getName");
                        if (n != null && n.Trim() == nombreDispositivo) { dispositivo = d; break; }
                    }
                    if (dispositivo == null)
                    {
                        estado = "BT: empareja '" + nombreDispositivo + "' en Ajustes > Bluetooth";
                        return;
                    }

                    estado = "BT: conectando a " + nombreDispositivo + "...";
                    AndroidJavaObject uuid;
                    using (var claseUuid = new AndroidJavaClass("java.util.UUID"))
                        uuid = claseUuid.CallStatic<AndroidJavaObject>("fromString", UUID_SPP);

                    // Nota: NO se llama cancelDiscovery(): en Android 12+ exige el permiso
                    // BLUETOOTH_SCAN y lanzaba una SecurityException.
                    var s = ConectarSocket(dispositivo, uuid);
                    if (s == null) return;

                    var entrada = s.Call<AndroidJavaObject>("getInputStream");
                    var salida = s.Call<AndroidJavaObject>("getOutputStream");
                    var lectorNuevo = new AndroidJavaObject("java.io.BufferedReader",
                        new AndroidJavaObject("java.io.InputStreamReader", entrada));
                    var escritorNuevo = new AndroidJavaObject("java.io.OutputStreamWriter", salida);

                    lock (candado)
                    {
                        socket = s;
                        lector = lectorNuevo;
                        escritor = escritorNuevo;
                    }
                    conectado = true;
                    estado = "BT: " + nombreDispositivo;
                }
            }
            catch (Exception e)
            {
                string m = e.Message ?? "";
                if (m.Contains("SecurityException"))
                    estado = "BT: permiso denegado: " + Resumir(m);
                else
                    estado = "BT: no conecta (" + Resumir(m) + ")";
                Cerrar();
            }
        }

        // Prueba dos formas de abrir el canal RFCOMM (algunos celulares con ESP32 solo aceptan una):
        //  1) socket seguro con UUID SPP   2) socket inseguro con UUID SPP
        AndroidJavaObject ConectarSocket(AndroidJavaObject dispositivo, AndroidJavaObject uuid)
        {
            string ultimoError = "";
            for (int intento = 0; intento < 2 && activo; intento++)
            {
                AndroidJavaObject s = null;
                try
                {
                    if (intento == 0)
                        s = dispositivo.Call<AndroidJavaObject>("createRfcommSocketToServiceRecord", uuid);
                    else
                        s = dispositivo.Call<AndroidJavaObject>("createInsecureRfcommSocketToServiceRecord", uuid);
                    estado = "BT: conectando a " + nombreDispositivo + " (método " + (intento + 1) + ")...";
                    s.Call("connect");   // bloqueante: por eso estamos en un hilo aparte
                    return s;
                }
                catch (Exception e)
                {
                    ultimoError = e.Message ?? "";
                    try { s?.Call("close"); } catch (Exception) { }
                    if (ultimoError.Contains("SecurityException")) break;
                }
            }
            estado = ultimoError.Contains("SecurityException") ? "BT: permiso denegado: " + Resumir(ultimoError) : "BT: no conecta (" + Resumir(ultimoError) + ")";
            return null;
        }

        static string Resumir(string m)
        {
            if (string.IsNullOrEmpty(m)) return "sin detalle";
            int i = m.IndexOf('\n');
            if (i > 0) m = m.Substring(0, i);
            m = m.Replace("java.lang.", "").Replace("java.io.", "");
            return m.Length > 70 ? m.Substring(0, 70) : m;
        }

        void Cerrar()
        {
            conectado = false;
            lock (candado)
            {
                try { socket?.Call("close"); } catch (Exception) { }
                socket = null; lector = null; escritor = null;
            }
        }
#else
        void Conectar() { }
        void Cerrar() { conectado = false; }
#endif
    }
}
