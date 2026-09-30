"""
Análisis de resultados - Teleoperación de Robot Móvil en RV (UMNG)
===================================================================
Lee los CSV que genera la app de Unity (carpeta "Metricas") y produce:
  - resumen_analisis.txt  : tabla con latencia, jitter, pérdidas, colisiones, tiempos de tarea
  - latencia_histograma.png, latencia_en_el_tiempo.png
  - periodo_tramas.png
  - mapeo_real_pitch_v.png  (verifica el mapeo pitch -> v y roll -> ω con datos reales)
  - trayectoria_comandos.png

Uso (Windows, con Python 3 instalado):
    pip install pandas matplotlib
    python analizar_metricas.py  "RUTA\\A\\LA\\CARPETA\\Metricas"
Si no se da la ruta, busca ../1_Unity_TeleoperacionRV/Metricas

En Android los CSV quedan en:
    Android/data/com.umng.teleoperacionrv/files/Metricas   (cópialos al PC por USB)
"""
import glob
import os
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd


def cargar(carpeta):
    muestras = sorted(glob.glob(os.path.join(carpeta, "muestras_*.csv")))
    eventos = sorted(glob.glob(os.path.join(carpeta, "eventos_*.csv")))
    if not muestras:
        sys.exit(f"No hay archivos muestras_*.csv en {carpeta}")
    dm, de = [], []
    for i, f in enumerate(muestras):
        d = pd.read_csv(f)
        d["sesion"] = os.path.basename(f)[9:-4]
        dm.append(d)
    for f in eventos:
        d = pd.read_csv(f)
        d["sesion"] = os.path.basename(f)[8:-4]
        de.append(d)
    return pd.concat(dm, ignore_index=True), (pd.concat(de, ignore_index=True) if de else pd.DataFrame())


def main():
    base = os.path.dirname(os.path.abspath(__file__))
    carpeta = sys.argv[1] if len(sys.argv) > 1 else os.path.join(base, "..", "1_Unity_TeleoperacionRV", "Metricas")
    salida = os.path.join(base, "resultados")
    os.makedirs(salida, exist_ok=True)
    m, e = cargar(carpeta)

    # Solo muestras con el control real conectado (no simulado con teclado)
    real = m[(m["conectado"] == 1) & (m["teclado"] == 0)]
    usar = real if len(real) > 0 else m
    lineas = [f"Sesiones analizadas: {m['sesion'].nunique()}",
              f"Muestras totales: {len(m)}  (con control real: {len(real)})", ""]

    # ---------------- Latencia ----------------
    # rtt_ms se repite hasta el siguiente ping: tomamos los cambios de valor
    rtt = usar["rtt_ms"]
    rtt = rtt[(rtt > 0) & (rtt.diff().fillna(1) != 0)]
    if len(rtt):
        p = np.percentile(rtt, [50, 95, 99])
        lineas += ["LATENCIA (ida y vuelta, RTT)",
                   f"  n = {len(rtt)}",
                   f"  media = {rtt.mean():.1f} ms   desviación = {rtt.std():.1f} ms",
                   f"  mediana = {p[0]:.1f} ms   p95 = {p[1]:.1f} ms   p99 = {p[2]:.1f} ms",
                   f"  mínimo = {rtt.min():.1f} ms   máximo = {rtt.max():.1f} ms",
                   f"  latencia de un sentido estimada (RTT/2) = {rtt.mean() / 2:.1f} ms", ""]
        plt.figure(figsize=(8, 4.5))
        plt.hist(rtt, bins=40, color="#1f4e79", alpha=0.85)
        plt.axvline(rtt.mean(), color="#d9822b", ls="--", label=f"media {rtt.mean():.1f} ms")
        plt.axvline(p[1], color="#b00", ls=":", label=f"p95 {p[1]:.1f} ms")
        plt.xlabel("RTT (ms)"); plt.ylabel("Frecuencia"); plt.title("Distribución de la latencia Bluetooth (ida y vuelta)")
        plt.legend(); plt.grid(alpha=0.3); plt.tight_layout()
        plt.savefig(os.path.join(salida, "latencia_histograma.png"), dpi=150); plt.close()

        plt.figure(figsize=(9, 4))
        t = usar.loc[rtt.index, "t_s"]
        plt.plot(t, rtt, ".", ms=3, color="#1f4e79")
        plt.xlabel("Tiempo de sesión (s)"); plt.ylabel("RTT (ms)"); plt.title("Latencia a lo largo de la prueba")
        plt.grid(alpha=0.3); plt.tight_layout()
        plt.savefig(os.path.join(salida, "latencia_en_el_tiempo.png"), dpi=150); plt.close()
    else:
        lineas += ["LATENCIA: sin datos (no hubo control real conectado)", ""]

    # ---------------- Periodo de tramas ----------------
    per = usar["periodo_trama_ms"]
    per = per[per > 0]
    if len(per):
        lineas += ["TRAMAS DEL CONTROL",
                   f"  periodo medio (filtrado) = {per.mean():.2f} ms  → {1000 / per.mean():.1f} Hz (ideal 50 Hz)", ""]
        plt.figure(figsize=(9, 4))
        plt.plot(usar.loc[per.index, "t_s"], per, color="#1f4e79", lw=0.8)
        plt.axhline(20, color="#d9822b", ls="--", label="ideal 20 ms")
        plt.xlabel("Tiempo (s)"); plt.ylabel("Periodo entre tramas (ms)"); plt.legend(); plt.grid(alpha=0.3)
        plt.title("Regularidad de la transmisión"); plt.tight_layout()
        plt.savefig(os.path.join(salida, "periodo_tramas.png"), dpi=150); plt.close()

    # ---------------- Mapeo real ----------------
    fig, ax = plt.subplots(1, 2, figsize=(12, 4.5))
    ax[0].scatter(usar["pitch_deg"], usar["v_ms"], s=3, alpha=0.4, color="#1f4e79")
    ax[0].set_xlabel("Pitch medido (°)"); ax[0].set_ylabel("v del robot (m/s)"); ax[0].set_title("Pitch → velocidad lineal"); ax[0].grid(alpha=0.3)
    ax[1].scatter(usar["roll_deg"], usar["w_degs"], s=3, alpha=0.4, color="#d9822b")
    ax[1].set_xlabel("Roll medido (°)"); ax[1].set_ylabel("ω del robot (°/s)"); ax[1].set_title("Roll → velocidad angular"); ax[1].grid(alpha=0.3)
    plt.tight_layout(); plt.savefig(os.path.join(salida, "mapeo_real_pitch_v.png"), dpi=150); plt.close()

    # Estabilidad: ruido del ángulo cuando la mano está quieta (comando = 0)
    quieto = usar[(usar["cmd_lineal"] == 0) & (usar["cmd_angular"] == 0)]
    if len(quieto) > 20:
        lineas += ["ESTABILIDAD (muestras en zona muerta)",
                   f"  desviación estándar pitch = {quieto['pitch_deg'].std():.2f}°   roll = {quieto['roll_deg'].std():.2f}°",
                   f"  % del tiempo con el robot quieto = {100 * len(quieto) / len(usar):.1f} %", ""]

    fig, ax = plt.subplots(3, 1, figsize=(10, 7), sharex=True)
    s0 = usar[usar["sesion"] == usar["sesion"].iloc[-1]]
    ax[0].plot(s0["t_s"], s0["pitch_deg"], label="pitch", color="#1f4e79"); ax[0].plot(s0["t_s"], s0["roll_deg"], label="roll", color="#d9822b")
    ax[0].set_ylabel("°"); ax[0].legend(); ax[0].grid(alpha=0.3)
    ax[1].plot(s0["t_s"], s0["v_ms"], color="#1f4e79"); ax[1].set_ylabel("v (m/s)"); ax[1].grid(alpha=0.3)
    ax[2].plot(s0["t_s"], s0["w_degs"], color="#d9822b"); ax[2].set_ylabel("ω (°/s)"); ax[2].set_xlabel("Tiempo (s)"); ax[2].grid(alpha=0.3)
    fig.suptitle("Última sesión: entrada del usuario y respuesta del robot"); plt.tight_layout()
    plt.savefig(os.path.join(salida, "trayectoria_comandos.png"), dpi=150); plt.close()

    # ---------------- Eventos ----------------
    if len(e):
        cnt = e["evento"].value_counts()
        lineas += ["EVENTOS (todas las sesiones)"] + [f"  {k}: {v}" for k, v in cnt.items()] + [""]
        comp = e[e["evento"] == "tarea_completada"]
        if len(comp):
            lineas += ["TIEMPO DE TAREA (llevar todos los objetos a la zona verde)",
                       f"  intentos completados = {len(comp)}",
                       f"  media = {comp['valor'].mean():.1f} s   mínimo = {comp['valor'].min():.1f} s   máximo = {comp['valor'].max():.1f} s", ""]
        vib = e[e["evento"] == "vibracion"]
        if len(vib):
            lineas += ["VIBRACIONES ENVIADAS"] + [f"  {k}: {v}" for k, v in vib["detalle"].value_counts().items()] + [""]

    texto = "\n".join(lineas)
    print(texto)
    with open(os.path.join(salida, "resumen_analisis.txt"), "w", encoding="utf-8") as f:
        f.write(texto)
    print(f"\nGráficas y resumen guardados en: {salida}")


if __name__ == "__main__":
    main()
