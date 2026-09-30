using System.Collections.Generic;
using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>Zona verde a la que hay que llevar los objetos (trigger).</summary>
    [RequireComponent(typeof(Collider))]
    public class ZonaObjetivo : MonoBehaviour
    {
        readonly HashSet<Manipulable> dentro = new HashSet<Manipulable>();

        void Awake() { GetComponent<Collider>().isTrigger = true; }

        void OnTriggerEnter(Collider c)
        {
            var m = c.attachedRigidbody ? c.attachedRigidbody.GetComponent<Manipulable>() : null;
            if (m && dentro.Add(m)) RegistroMetricas.Evento("entra_zona", m.name, 0);
        }

        void OnTriggerExit(Collider c)
        {
            var m = c.attachedRigidbody ? c.attachedRigidbody.GetComponent<Manipulable>() : null;
            if (m && dentro.Remove(m)) RegistroMetricas.Evento("sale_zona", m.name, 0);
        }

        /// <summary>Objetos de la misión que están dentro y ya fueron soltados.</summary>
        public int Contar()
        {
            int n = 0;
            dentro.RemoveWhere(m => m == null);
            foreach (var m in dentro) if (m.cuentaParaMision && !m.Sostenido) n++;
            return n;
        }

        public void Limpiar() { dentro.Clear(); }
    }
}
