# Teleoperación de Robot Móvil en Entorno Virtual con Control Háptico Inalámbrico

Proyecto de Realidad Virtual (Ingeniería Mecatrónica, UMNG, 2026).

| Carpeta | Qué contiene |
|---|---|
| `1_Unity_TeleoperacionRV` | Proyecto Unity 6 (6000.3.8f1). Al abrirlo se construye la escena **Teleoperacion**. Menú **Teleoperación RV** para reconstruir la escena, ajustar Android y generar el APK. |
| `2_Firmware_ESP32/ControlHaptico` | Firmware del control (Arduino IDE, placa *ESP32 Dev Module*, core esp32 3.x). |
| `3_Carcasa_CAD` | Carcasa paramétrica en OpenSCAD + STL de base y tapa + imágenes. |
| `4_Diagramas` | Arquitectura, diagrama eléctrico, diagrama de comunicación, gráfica de mapeo. |
| `5_Analisis_Resultados` | `analizar_metricas.py`: convierte los CSV de la app en gráficas y estadísticas. |
| `6_Informe` | Informe técnico (Word) y **Guía paso a paso** (empieza por aquí). |

## Prueba rápida en el PC
1. Abrir el proyecto en Unity Hub y presionar **Play**.
2. Sin control: **W/S** avanzar/retroceder, **A/D** girar, **G** agarrar/soltar, **↑/↓** caminar, **←/→** girar, **clic derecho + ratón** mirar, **R** reiniciar, **M** estéreo.

## Protocolo Bluetooth (SPP, texto)
- ESP32 → Unity: `D,seq,pitch,roll,agarre,adelante,atras,bateria,joyY,joyX` (50 Hz; agarre = palanca del joystick presionada, joyY y joyX = -100..100), `P,id`, `I,...`, `K`
- Unity → ESP32: `H,patron,intensidad` (1 objeto, 2 pared, 3 agarre), `P,id`, `C`
