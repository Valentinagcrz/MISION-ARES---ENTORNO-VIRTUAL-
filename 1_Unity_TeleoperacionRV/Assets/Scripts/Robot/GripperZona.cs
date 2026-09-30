using System.Collections.Generic;
using UnityEngine;

namespace TeleoperacionRV
{
    /// <summary>Zona (trigger) frente a la pinza: sabe qué objetos se pueden agarrar.</summary>
    [RequireComponent(typeof(Collider))]
    public class GripperZona : MonoBehaviour
    {
        readonly HashSet<Manipulable> dentro = new HashSet<Manipulable>();
        Manipulable resaltado;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider c)
        {
            var m = c.attachedRigidbody ? c.attachedRigidbody.GetComponent<Manipulable>() : null;
            if (m) dentro.Add(m);
        }

        void OnTriggerExit(Collider c)
        {
            var m = c.attachedRigidbody ? c.attachedRigidbody.GetComponent<Manipulable>() : null;
            if (m) { dentro.Remove(m); m.Resaltar(false); }
        }

        public Manipulable MasCercano(Vector3 punto)
        {
            Manipulable mejor = null;
            float mejorD = float.MaxValue;
            dentro.RemoveWhere(m => m == null);
            foreach (var m in dentro)
            {
                if (m.Sostenido) continue;
                float d = (m.transform.position - punto).sqrMagnitude;
                if (d < mejorD) { mejorD = d; mejor = m; }
            }
            return mejor;
        }

        /// <summary>Resalta en amarillo el objeto que se agarraría ahora.</summary>
        public void ActualizarResaltado(Vector3 punto, bool permitir)
        {
            var m = permitir ? MasCercano(punto) : null;
            if (m == resaltado) return;
            if (resaltado) resaltado.Resaltar(false);
            resaltado = m;
            if (resaltado) resaltado.Resaltar(true);
        }
    }
}
