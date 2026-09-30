using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>
    /// Estado de la simulación que el celular envía al computador (modo espectador)
    /// por Wi-Fi (UDP). Formato binario compacto, ~300 bytes, 30 veces por segundo.
    /// </summary>
    public class PaqueteEspejo
    {
        public const int Puerto = 47777;
        static readonly byte[] Firma = { (byte)'T', (byte)'R', (byte)'V', (byte)'1' };
        public static readonly byte[] Confirmacion = { (byte)'T', (byte)'R', (byte)'V', (byte)'A' };

        public int secuencia;
        public float tiempo;
        // Robot
        public Vector3 robotPos; public Quaternion robotRot; public float apertura;
        public float velLineal, velAngular;
        // Avatar
        public Vector3 avatarPos; public float avatarYaw;
        public Vector3 cabezaPos; public Quaternion cabezaRot;
        // Objetos manipulables (ordenados por nombre)
        public Vector3[] objPos = new Vector3[0];
        public Quaternion[] objRot = new Quaternion[0];
        public bool[] objSostenido = new bool[0];
        public string sostenidoNombre = "";
        // Misión
        public int enZona, total; public float tiempoTarea; public bool iniciada, completada;
        // Control háptico
        public bool conectado; public float pitch, roll, joyX, joyY, rtt; public int bateria;
        public int ultimaVibracion; public float edadVibracion;

        /// <summary>Objetos manipulables de la escena en un orden fijo (igual en celular y PC).</summary>
        public static Manipulable[] ObjetosOrdenados() =>
            UnityEngine.Object.FindObjectsByType<Manipulable>(FindObjectsSortMode.None).OrderBy(m => m.name, StringComparer.Ordinal).ToArray();

        // ------------------------------------------------------------------
        static void W(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        static void W(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
        static Vector3 V(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        static Quaternion Q(BinaryReader r) => new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        public byte[] Serializar()
        {
            using (var ms = new MemoryStream(512))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Firma);
                w.Write(secuencia); w.Write(tiempo);
                W(w, robotPos); W(w, robotRot); w.Write(apertura); w.Write(velLineal); w.Write(velAngular);
                W(w, avatarPos); w.Write(avatarYaw); W(w, cabezaPos); W(w, cabezaRot);
                w.Write((byte)objPos.Length);
                for (int i = 0; i < objPos.Length; i++) { W(w, objPos[i]); W(w, objRot[i]); w.Write(objSostenido[i]); }
                w.Write(sostenidoNombre ?? "");
                w.Write((byte)enZona); w.Write((byte)total); w.Write(tiempoTarea); w.Write(iniciada); w.Write(completada);
                w.Write(conectado); w.Write(pitch); w.Write(roll); w.Write(joyX); w.Write(joyY); w.Write(rtt);
                w.Write((short)bateria); w.Write((byte)ultimaVibracion); w.Write(edadVibracion);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static PaqueteEspejo Leer(byte[] datos)
        {
            if (datos == null || datos.Length < 8) return null;
            for (int i = 0; i < 4; i++) if (datos[i] != Firma[i]) return null;
            try
            {
                using (var r = new BinaryReader(new MemoryStream(datos, 4, datos.Length - 4)))
                {
                    var p = new PaqueteEspejo();
                    p.secuencia = r.ReadInt32(); p.tiempo = r.ReadSingle();
                    p.robotPos = V(r); p.robotRot = Q(r); p.apertura = r.ReadSingle(); p.velLineal = r.ReadSingle(); p.velAngular = r.ReadSingle();
                    p.avatarPos = V(r); p.avatarYaw = r.ReadSingle(); p.cabezaPos = V(r); p.cabezaRot = Q(r);
                    int n = r.ReadByte();
                    p.objPos = new Vector3[n]; p.objRot = new Quaternion[n]; p.objSostenido = new bool[n];
                    for (int i = 0; i < n; i++) { p.objPos[i] = V(r); p.objRot[i] = Q(r); p.objSostenido[i] = r.ReadBoolean(); }
                    p.sostenidoNombre = r.ReadString();
                    p.enZona = r.ReadByte(); p.total = r.ReadByte(); p.tiempoTarea = r.ReadSingle(); p.iniciada = r.ReadBoolean(); p.completada = r.ReadBoolean();
                    p.conectado = r.ReadBoolean(); p.pitch = r.ReadSingle(); p.roll = r.ReadSingle(); p.joyX = r.ReadSingle(); p.joyY = r.ReadSingle(); p.rtt = r.ReadSingle();
                    p.bateria = r.ReadInt16(); p.ultimaVibracion = r.ReadByte(); p.edadVibracion = r.ReadSingle();
                    return p;
                }
            }
            catch (Exception) { return null; }
        }
    }
}
