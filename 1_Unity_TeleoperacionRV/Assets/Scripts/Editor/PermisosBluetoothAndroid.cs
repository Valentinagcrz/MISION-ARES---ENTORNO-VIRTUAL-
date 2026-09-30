#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace TeleoperacionRV.EditorTools
{
    /// <summary>
    /// Agrega al AndroidManifest los permisos de Bluetooth que Unity no pone solo:
    ///   BLUETOOTH y BLUETOOTH_ADMIN (Android 11 o menor) y BLUETOOTH_CONNECT (Android 12+).
    /// Se ejecuta automáticamente al generar el APK.
    /// </summary>
    public class PermisosBluetoothAndroid : IPostGenerateGradleAndroidProject
    {
        const string NS = "http://schemas.android.com/apk/res/android";
        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string ruta)
        {
            string manifiesto = Path.Combine(ruta, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifiesto))
            {
                Debug.LogWarning("[PermisosBluetooth] No se encontró " + manifiesto);
                return;
            }
            var doc = new XmlDocument();
            doc.Load(manifiesto);
            var raiz = doc.DocumentElement;
            Agregar(doc, raiz, "android.permission.BLUETOOTH", 30);
            Agregar(doc, raiz, "android.permission.BLUETOOTH_ADMIN", 30);
            Agregar(doc, raiz, "android.permission.BLUETOOTH_CONNECT", 0);
            doc.Save(manifiesto);
            Debug.Log("[PermisosBluetooth] Permisos de Bluetooth agregados al AndroidManifest.");
        }

        static void Agregar(XmlDocument doc, XmlElement raiz, string permiso, int maxSdk)
        {
            foreach (XmlNode n in raiz.SelectNodes("uses-permission"))
            {
                var a = n.Attributes?["android:name"];
                if (a != null && a.Value == permiso) return;
            }
            var e = doc.CreateElement("uses-permission");
            e.SetAttribute("name", NS, permiso);
            if (maxSdk > 0) e.SetAttribute("maxSdkVersion", NS, maxSdk.ToString());
            raiz.PrependChild(e);
        }
    }
}
#endif
