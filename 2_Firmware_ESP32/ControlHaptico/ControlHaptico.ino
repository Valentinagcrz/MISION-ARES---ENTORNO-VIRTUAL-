/*
  ============================================================================
   CONTROL HÁPTICO INALÁMBRICO - Teleoperación de Robot Móvil en RV (UMNG)
  ============================================================================
   Placa ..........: ESP32 Dev Module (ESP32-WROOM-32, ESP32 "clásico").
                     OJO: ESP32-S3 / C3 / C6 NO tienen Bluetooth clásico.
   Core Arduino ...: arduino-esp32 3.x (verificado con 3.3.8). También compila en 2.x.
   Librerías ......: ninguna externa (MPU6050 leído por registros, BluetoothSerial incluida).

   Qué hace:
     1. Lee la IMU MPU6050 a 200 Hz y estima Pitch y Roll con un filtro
        complementario (giróscopo + acelerómetro).
     2. Lee el joystick HW-504: eje Y = avatar adelante/atrás, eje X = girar el
        avatar, presionar la palanca (SW) = agarrar/soltar.
     3. Envía el estado a Unity por Bluetooth SPP (y por USB) a 50 Hz.
     4. Recibe órdenes de vibración desde Unity y las reproduce con PWM
        en el módulo de motor vibrador (patrones distintos para objeto / pared / agarre).
     5. Mide la batería (divisor resistivo) y responde "pings" para medir latencia.

   Protocolo (texto, una línea por mensaje, terminada en '\n'):
     ESP32 -> Unity
       I,ControlHaptico_RV,<version>          al conectar / al pedir "?"
       D,<seq>,<pitch>,<roll>,<agarre>,<adelante>,<atras>,<bateria%>,<joyY>,<joyX>
                                              50 veces por segundo. Ángulos en grados.
                                              joyY, joyX: -100..100 (ejes del joystick);
                                              adelante/atras = joyY pasó el umbral.
       P,<id>                                 eco de un ping (medición de latencia)
       K                                      confirmación de calibración
     Unity -> ESP32
       H,<patron>,<intensidad 0-255>          vibrar. patron: 1=objeto 2=pared 3=agarre 0=parar
       P,<id>                                 ping
       C                                      calibrar "cero" (posición neutra de la mano)
       ?                                      pedir identificación
  ============================================================================
*/

#include <Arduino.h>
#include <Wire.h>
#include "BluetoothSerial.h"

#if !defined(CONFIG_BT_ENABLED) || !defined(CONFIG_BLUEDROID_ENABLED)
#error "Bluetooth no disponible. Selecciona la placa 'ESP32 Dev Module' (ESP32 clasico, no S2/S3/C3)."
#endif
#if !defined(CONFIG_BT_SPP_ENABLED)
#error "Bluetooth SPP no disponible en esta placa. Usa un ESP32-WROOM-32 (ESP32 Dev Module)."
#endif

// ----------------------------------------------------------------------------
//  CONFIGURACIÓN (cámbiala según tu montaje)
// ----------------------------------------------------------------------------
#define NOMBRE_BLUETOOTH   "ControlHaptico_RV"
#define VERSION_FIRMWARE   "1.0"

// Pines (ESP32 DevKit V1)
#define PIN_SDA            21
#define PIN_SCL            22
// Joystick HW-504: GND->GND, +5V->**3V3** (¡no a 5 V!), VRx->32, VRy->35, SW->25
#define PIN_JOY_SW         25   // pulsador de la palanca a GND (usa pull-up interno)
#define PIN_JOY_Y          35   // ADC1 (solo entrada)
#define PIN_JOY_X          32   // ADC1 (girar el avatar)
// Módulo "Vibration Motor" (motor de moneda con transistor incluido):
//   IN -> GPIO23   VCC -> 3V3   GND -> GND
#define PIN_MOTOR          23   // al pin IN del módulo (PWM)
#define PIN_BATERIA        34   // punto medio del divisor 100k/22k desde BAT+ (12 V)
#define PIN_LED            2    // LED azul integrado de la placa

// IMU
// La MPU6050 responde en 0x68 (AD0 a GND) o 0x69 (AD0 al aire / 3.3 V).
// El programa prueba las dos automáticamente.
// Si al inclinar hacia adelante el valor de pitch sale negativo, cambia a -1.
#define SIGNO_PITCH        (+1)
#define SIGNO_ROLL         (+1)
// Si la IMU quedó girada 90° en la carcasa, pon 1 para intercambiar los ejes.
#define INTERCAMBIAR_EJES  0
#define ALFA_FILTRO        0.98f  // filtro complementario (0.95 - 0.99)

// Joystick
#define SIGNO_JOY          (-1)   // invertido: con el joystick montado así, empujar al frente daba negativo
#define SIGNO_JOY_X        (+1)   // si empujar a la derecha da valores negativos, pon -1
#define JOY_ZONA_MUERTA    12     // % alrededor del centro que se ignora
#define JOY_UMBRAL_ON      40     // % para activar adelante/atrás
#define JOY_UMBRAL_OFF     25     // % para desactivar (histéresis)

// Vibración
#define FRECUENCIA_PWM     5000   // Hz
#define RESOLUCION_PWM     8      // bits -> duty 0..255
#define DUTY_MINIMO        185    // piso de fuerza: toda vibración arranca al menos aquí (antes 110)
#define DUTY_MAXIMO        255
// La "intensidad" que manda Unity (0-255) solo ajusta la fuerza entre
// DUTY_MINIMO y DUTY_MAXIMO, así hasta el choque más suave se siente claro.
// Para que vibre AÚN más fuerte: pasa el VCC del módulo de 3V3 a 5 V
// (la salida del LM2596S). El módulo acepta 3.3-5 V y el pin IN sigue en GPIO23.

// Batería de 12 V + regulador LM2596S (ajustado a 5.0 V) -> VIN del ESP32.
// Divisor: BAT+ (después del interruptor) -> 100 kΩ -> GPIO34 -> 22 kΩ -> GND
//   factor = (100 + 22) / 22 = 5.545   (13 V -> 2.34 V en el ADC, seguro)
// NUNCA conectes los 12 V directo a un pin del ESP32.
#define USAR_BATERIA       1      // 0 si no conectaste el divisor
#define FACTOR_DIVISOR     5.545f
// Voltajes para el %: valores para batería de litio 3S (9.6 V vacía, 12.6 V llena).
// Si tu batería es de plomo (12 V "de alarma"): VACIA 11500, LLENA 12800.
// Si es de 8 pilas AA recargables: VACIA 8800, LLENA 11200.
#define BAT_MV_VACIA       9600.0f
#define BAT_MV_LLENA       12600.0f

// Tiempos
#define PERIODO_IMU_US     5000   // 200 Hz
#define PERIODO_ENVIO_MS   20     // 50 Hz
#define REBOTE_MS          25

// ----------------------------------------------------------------------------
//  Compatibilidad PWM entre core 2.x y 3.x
// ----------------------------------------------------------------------------
#if defined(ESP_ARDUINO_VERSION_MAJOR) && ESP_ARDUINO_VERSION_MAJOR >= 3
  static void pwmIniciar()            { ledcAttach(PIN_MOTOR, FRECUENCIA_PWM, RESOLUCION_PWM); }
  static void pwmEscribir(uint32_t d) { ledcWrite(PIN_MOTOR, d); }
#else
  #define CANAL_PWM 0
  static void pwmIniciar()            { ledcSetup(CANAL_PWM, FRECUENCIA_PWM, RESOLUCION_PWM); ledcAttachPin(PIN_MOTOR, CANAL_PWM); }
  static void pwmEscribir(uint32_t d) { ledcWrite(CANAL_PWM, d); }
#endif

// ----------------------------------------------------------------------------
//  Prototipos
// ----------------------------------------------------------------------------
static bool  imuIniciar();
static bool  imuLeer(float &ax, float &ay, float &az, float &gx, float &gy, float &gz);
static void  imuCalibrarGiro();
static void  imuActualizar();
static void  calibrarCero();
static void  botonesActualizar();
static void  joystickCalibrar();
static void  joystickActualizar();
static void  hapticaReproducir(uint8_t patron, uint8_t intensidad);
static void  hapticaActualizar();
static void  bateriaActualizar();
static void  ledActualizar();
static void  enviarEstado();
static void  enviarLinea(const char *linea);
static void  procesarEntrada(Stream &s, char *buf, size_t &len, bool esBT);
static void  procesarComando(char *cmd, Stream &origen);

BluetoothSerial SerialBT;

// ----------------------------------------------------------------------------
//  IMU MPU6050 (lectura directa de registros)
// ----------------------------------------------------------------------------
static const float ESCALA_ACC  = 8192.0f;  // ±4 g
static const float ESCALA_GIRO = 65.5f;    // ±500 °/s
static float sesgoGx = 0, sesgoGy = 0, sesgoGz = 0;
static float pitchFiltrado = 0, rollFiltrado = 0;   // ángulos absolutos del filtro
static float pitchCero = 0, rollCero = 0;           // posición neutra de la mano
static uint32_t tImuAnterior = 0;
static bool imuOk = false;
static uint8_t DIRECCION_MPU = 0x68;   // se detecta en imuIniciar()

static void escribirRegistro(uint8_t reg, uint8_t valor) {
  Wire.beginTransmission(DIRECCION_MPU);
  Wire.write(reg);
  Wire.write(valor);
  Wire.endTransmission();
}

static bool imuIniciar() {
  Wire.begin(PIN_SDA, PIN_SCL);
  Wire.setClock(400000);
  const uint8_t candidatas[2] = {0x68, 0x69};
  bool encontrada = false;
  for (uint8_t d : candidatas) {
    Wire.beginTransmission(d);
    if (Wire.endTransmission() == 0) { DIRECCION_MPU = d; encontrada = true; break; }
  }
  if (!encontrada) return false;                    // no responde en ninguna
  escribirRegistro(0x6B, 0x01);  // PWR_MGMT_1: despertar, reloj PLL giro X
  delay(50);
  escribirRegistro(0x19, 0x04);  // SMPLRT_DIV: 1 kHz / (1+4) = 200 Hz
  escribirRegistro(0x1A, 0x03);  // CONFIG: filtro pasa bajas digital ~44 Hz
  escribirRegistro(0x1B, 0x08);  // GYRO_CONFIG: ±500 °/s
  escribirRegistro(0x1C, 0x08);  // ACCEL_CONFIG: ±4 g
  delay(20);
  return true;
}

static bool imuLeer(float &ax, float &ay, float &az, float &gx, float &gy, float &gz) {
  Wire.beginTransmission(DIRECCION_MPU);
  Wire.write(0x3B);
  if (Wire.endTransmission(false) != 0) return false;
  if (Wire.requestFrom((uint8_t)DIRECCION_MPU, (uint8_t)14) != 14) return false;
  int16_t r[7];
  for (int i = 0; i < 7; i++) r[i] = (int16_t)((Wire.read() << 8) | Wire.read());
  float rax = r[0] / ESCALA_ACC, ray = r[1] / ESCALA_ACC;
  float rgx = r[4] / ESCALA_GIRO, rgy = r[5] / ESCALA_GIRO;
#if INTERCAMBIAR_EJES
  ax = ray;  ay = -rax; gx = rgy; gy = -rgx;
#else
  ax = rax;  ay = ray;  gx = rgx; gy = rgy;
#endif
  az = r[2] / ESCALA_ACC;
  gz = r[6] / ESCALA_GIRO;      // r[3] es temperatura
  return true;
}

// Promedia el giróscopo en reposo para quitarle el sesgo (deja el control quieto).
static void imuCalibrarGiro() {
  const int N = 400;
  double sx = 0, sy = 0, sz = 0;
  int validas = 0;
  for (int i = 0; i < N; i++) {
    float ax, ay, az, gx, gy, gz;
    if (imuLeer(ax, ay, az, gx, gy, gz)) { sx += gx; sy += gy; sz += gz; validas++; }
    digitalWrite(PIN_LED, (i / 20) % 2);   // parpadeo rápido = calibrando
    delay(3);
  }
  if (validas > 0) { sesgoGx = sx / validas; sesgoGy = sy / validas; sesgoGz = sz / validas; }
  // Inicializa el filtro con el acelerómetro
  float ax, ay, az, gx, gy, gz;
  if (imuLeer(ax, ay, az, gx, gy, gz)) {
    pitchFiltrado = atan2f(-ax, sqrtf(ay * ay + az * az)) * RAD_TO_DEG;
    rollFiltrado  = atan2f(ay, az) * RAD_TO_DEG;
  }
  tImuAnterior = micros();
}

// Filtro complementario:  ángulo = α·(ángulo + ω·dt) + (1-α)·ángulo_acelerómetro
static void imuActualizar() {
  float ax, ay, az, gx, gy, gz;
  if (!imuLeer(ax, ay, az, gx, gy, gz)) return;
  uint32_t ahora = micros();
  float dt = (ahora - tImuAnterior) * 1e-6f;
  tImuAnterior = ahora;
  if (dt <= 0 || dt > 0.1f) dt = PERIODO_IMU_US * 1e-6f;

  float pitchAcc = atan2f(-ax, sqrtf(ay * ay + az * az)) * RAD_TO_DEG;  // nariz abajo = +
  float rollAcc  = atan2f(ay, az) * RAD_TO_DEG;                          // lado derecho abajo = +
  float velPitch = gy - sesgoGy;
  float velRoll  = gx - sesgoGx;

  pitchFiltrado = ALFA_FILTRO * (pitchFiltrado + velPitch * dt) + (1.0f - ALFA_FILTRO) * pitchAcc;
  rollFiltrado  = ALFA_FILTRO * (rollFiltrado  + velRoll  * dt) + (1.0f - ALFA_FILTRO) * rollAcc;
}

// Toma la postura actual de la mano como "cero" (robot quieto).
static void calibrarCero() {
  const int N = 100;
  float sp = 0, sr = 0;
  for (int i = 0; i < N; i++) {
    imuActualizar();
    sp += pitchFiltrado; sr += rollFiltrado;
    delayMicroseconds(PERIODO_IMU_US);
  }
  pitchCero = sp / N;
  rollCero  = sr / N;
}

// ----------------------------------------------------------------------------
//  Botones (con anti-rebote)
// ----------------------------------------------------------------------------
struct Boton { uint8_t pin; bool estado; bool lectura; uint32_t tCambio; };
static Boton botones[1] = {
  {PIN_JOY_SW, false, false, 0},     // presionar la palanca = AGARRE
};

static void botonesActualizar() {
  uint32_t ahora = millis();
  for (auto &b : botones) {
    bool l = digitalRead(b.pin) == LOW;       // presionado = LOW (pull-up)
    if (l != b.lectura) { b.lectura = l; b.tCambio = ahora; }
    if ((ahora - b.tCambio) >= REBOTE_MS) b.estado = b.lectura;
  }
}

// ----------------------------------------------------------------------------
//  Joystick (eje Y -> -100..100 con zona muerta e histéresis)
// ----------------------------------------------------------------------------
static float joyCentroY = 2048, joyCentroX = 2048;
static float joyY = 0, joyX = 0;       // -100..100
static bool  joyAdelante = false, joyAtras = false;

static void joystickCalibrar() {
  long sy = 0, sx = 0;
  for (int i = 0; i < 64; i++) { sy += analogRead(PIN_JOY_Y); sx += analogRead(PIN_JOY_X); delay(2); }
  joyCentroY = sy / 64.0f;
  joyCentroX = sx / 64.0f;
}

// Convierte una lectura del ADC (0..4095) a -100..100 con zona muerta
static float joyEje(float raw, float centro) {
  float v;
  if (raw >= centro) v = (raw - centro) * 100.0f / max(1.0f, 4095.0f - centro);
  else               v = (raw - centro) * 100.0f / max(1.0f, centro);
  if (fabsf(v) < JOY_ZONA_MUERTA) return 0;
  v = (v > 0 ? 1 : -1) * (fabsf(v) - JOY_ZONA_MUERTA) * 100.0f / (100.0f - JOY_ZONA_MUERTA);
  return constrain(v, -100.0f, 100.0f);
}

static void joystickActualizar() {
  joyY = joyY * 0.6f + SIGNO_JOY   * joyEje(analogRead(PIN_JOY_Y), joyCentroY) * 0.4f;
  joyX = joyX * 0.6f + SIGNO_JOY_X * joyEje(analogRead(PIN_JOY_X), joyCentroX) * 0.4f;
  if (joyY > JOY_UMBRAL_ON) joyAdelante = true; else if (joyY < JOY_UMBRAL_OFF) joyAdelante = false;
  if (joyY < -JOY_UMBRAL_ON) joyAtras = true;   else if (joyY > -JOY_UMBRAL_OFF) joyAtras = false;
}

// ----------------------------------------------------------------------------
//  Háptica: patrones de vibración no bloqueantes
// ----------------------------------------------------------------------------
struct Paso { uint8_t duty; uint16_t ms; };
// 1 = OBJETO: tres toques marcados  ("tac-tac-tac")
static const Paso PATRON_OBJETO[]  = { {230, 110}, {0, 70}, {230, 110}, {0, 70}, {230, 110} };
// 2 = PARED / LÍMITE: zumbido fuerte y largo + remate
static const Paso PATRON_PARED[]   = { {255, 500}, {0, 80}, {255, 250} };
// 3 = AGARRE: un "clic" corto y firme de confirmación
static const Paso PATRON_AGARRE[]  = { {255, 110} };
// 4 = CONEXIÓN: dos pulsos al conectarse Unity
static const Paso PATRON_CONEXION[] = { {240, 150}, {0, 100}, {240, 150} };

static const Paso *patronActual = nullptr;
static uint8_t  pasosPatron = 0, pasoActual = 0, prioridadActual = 0;
static uint8_t  escalaIntensidad = 255;
static uint32_t tInicioPaso = 0;

static uint8_t prioridadDe(uint8_t patron) {
  switch (patron) { case 2: return 3; case 1: return 2; default: return 1; }
}

static void motorDuty(uint8_t duty) {
  if (duty == 0) { pwmEscribir(0); return; }
  // fuerza del paso (0-255) x intensidad pedida -> se lleva al rango [DUTY_MINIMO, DUTY_MAXIMO]
  uint32_t f = (uint32_t)duty * escalaIntensidad / 255;
  uint32_t d = DUTY_MINIMO + f * (DUTY_MAXIMO - DUTY_MINIMO) / 255;
  pwmEscribir(d);
}

static void hapticaReproducir(uint8_t patron, uint8_t intensidad) {
  if (patron == 0) { patronActual = nullptr; prioridadActual = 0; pwmEscribir(0); return; }
  // Un patrón más importante no se interrumpe por uno menos importante
  if (patronActual && prioridadDe(patron) < prioridadActual) return;
  switch (patron) {
    case 1: patronActual = PATRON_OBJETO;   pasosPatron = sizeof(PATRON_OBJETO) / sizeof(Paso); break;
    case 2: patronActual = PATRON_PARED;    pasosPatron = sizeof(PATRON_PARED) / sizeof(Paso); break;
    case 3: patronActual = PATRON_AGARRE;   pasosPatron = sizeof(PATRON_AGARRE) / sizeof(Paso); break;
    case 4: patronActual = PATRON_CONEXION; pasosPatron = sizeof(PATRON_CONEXION) / sizeof(Paso); break;
    default: return;
  }
  prioridadActual  = prioridadDe(patron);
  escalaIntensidad = intensidad == 0 ? 255 : intensidad;
  pasoActual = 0;
  tInicioPaso = millis();
  motorDuty(patronActual[0].duty);
}

static void hapticaActualizar() {
  if (!patronActual) return;
  if (millis() - tInicioPaso < patronActual[pasoActual].ms) return;
  pasoActual++;
  if (pasoActual >= pasosPatron) { patronActual = nullptr; prioridadActual = 0; pwmEscribir(0); return; }
  tInicioPaso = millis();
  motorDuty(patronActual[pasoActual].duty);
}

// ----------------------------------------------------------------------------
//  Batería
// ----------------------------------------------------------------------------
static float bateriaMv = 0;
static int   bateriaPorc = -1;

static void bateriaActualizar() {
#if USAR_BATERIA
  float mv = analogReadMilliVolts(PIN_BATERIA) * FACTOR_DIVISOR;
  bateriaMv = (bateriaMv <= 0) ? mv : bateriaMv * 0.95f + mv * 0.05f;   // suavizado
  float p = (bateriaMv - BAT_MV_VACIA) * 100.0f / (BAT_MV_LLENA - BAT_MV_VACIA);
  bateriaPorc = (int)constrain(p, 0.0f, 100.0f);
#endif
}

// ----------------------------------------------------------------------------
//  LED de estado: parpadeo lento = esperando conexión, fijo = conectado
// ----------------------------------------------------------------------------
static void ledActualizar() {
  if (!imuOk) { digitalWrite(PIN_LED, (millis() / 100) % 2); return; }   // error IMU: muy rápido
  if (SerialBT.hasClient()) digitalWrite(PIN_LED, HIGH);
  else digitalWrite(PIN_LED, (millis() / 500) % 2);
}

// ----------------------------------------------------------------------------
//  Comunicación
// ----------------------------------------------------------------------------
static uint32_t secuencia = 0;

static void enviarLinea(const char *linea) {
  Serial.print(linea);                               // USB (útil para pruebas con cable)
  if (SerialBT.hasClient()) SerialBT.print(linea);   // Bluetooth
}

static void enviarEstado() {
  char buf[112];
  snprintf(buf, sizeof(buf), "D,%lu,%.1f,%.1f,%d,%d,%d,%d,%d,%d\n",
           (unsigned long)secuencia++,
           SIGNO_PITCH * (pitchFiltrado - pitchCero),
           SIGNO_ROLL  * (rollFiltrado  - rollCero),
           botones[0].estado ? 1 : 0,
           joyAdelante ? 1 : 0,
           joyAtras ? 1 : 0,
           bateriaPorc,
           (int)lroundf(joyY),
           (int)lroundf(joyX));
  enviarLinea(buf);
}

static void procesarComando(char *cmd, Stream &origen) {
  if (cmd[0] == 'H') {                         // H,patron,intensidad
    int patron = 0, intensidad = 255;
    sscanf(cmd, "H,%d,%d", &patron, &intensidad);
    hapticaReproducir((uint8_t)constrain(patron, 0, 4), (uint8_t)constrain(intensidad, 0, 255));
  } else if (cmd[0] == 'P') {                  // P,id  -> eco inmediato por el mismo canal
    origen.print(cmd);
    origen.print('\n');
  } else if (cmd[0] == 'C') {
    calibrarCero();
    origen.print("K\n");
  } else if (cmd[0] == '?') {
    origen.print("I," NOMBRE_BLUETOOTH "," VERSION_FIRMWARE "\n");
  }
}

static void procesarEntrada(Stream &s, char *buf, size_t &len, bool esBT) {
  (void)esBT;
  while (s.available()) {
    char c = (char)s.read();
    if (c == '\r') continue;
    if (c == '\n') {
      buf[len] = 0;
      if (len > 0) procesarComando(buf, s);
      len = 0;
    } else if (len < 47) {
      buf[len++] = c;
    } else {
      len = 0;   // línea demasiado larga: se descarta
    }
  }
}

// ----------------------------------------------------------------------------
//  SETUP / LOOP
// ----------------------------------------------------------------------------
static char bufUSB[48], bufBT[48];
static size_t lenUSB = 0, lenBT = 0;
static bool clienteAnterior = false;
static uint32_t tImu = 0, tEnvio = 0, tBateria = 0, tJoy = 0;

void setup() {
  Serial.begin(115200);
  pinMode(PIN_LED, OUTPUT);
  for (auto &b : botones) pinMode(b.pin, INPUT_PULLUP);
  pwmIniciar();
  pwmEscribir(0);
#if USAR_BATERIA
  analogSetPinAttenuation(PIN_BATERIA, ADC_11db);
#endif
  analogSetPinAttenuation(PIN_JOY_Y, ADC_11db);
  analogSetPinAttenuation(PIN_JOY_X, ADC_11db);
  joystickCalibrar();   // la palanca debe estar suelta (centrada) al encender

  imuOk = imuIniciar();
  if (!imuOk) {
    Serial.println("# ERROR: no se encontro la MPU6050 (ni 0x68 ni 0x69). Revisa SDA=21, SCL=22, VCC=3.3V, GND.");
  } else {
    Serial.printf("# MPU6050 encontrada en 0x%02X\n", DIRECCION_MPU);
    Serial.println("# Calibrando: deja el control QUIETO en posicion neutra...");
    imuCalibrarGiro();
    calibrarCero();
    Serial.println("# Calibracion lista.");
  }

  SerialBT.begin(NOMBRE_BLUETOOTH);
  Serial.println("# Bluetooth listo: empareja el dispositivo '" NOMBRE_BLUETOOTH "'");
  Serial.print("I," NOMBRE_BLUETOOTH "," VERSION_FIRMWARE "\n");
  hapticaReproducir(3, 255);   // pequeño pulso: encendido OK
}

void loop() {
  uint32_t ahoraUs = micros();
  uint32_t ahoraMs = millis();

  if (imuOk && (ahoraUs - tImu) >= PERIODO_IMU_US) {
    tImu = ahoraUs;
    imuActualizar();
  }

  botonesActualizar();
  hapticaActualizar();
  if ((ahoraMs - tJoy) >= 10) { tJoy = ahoraMs; joystickActualizar(); }
  procesarEntrada(Serial, bufUSB, lenUSB, false);
  procesarEntrada(SerialBT, bufBT, lenBT, true);

  bool cliente = SerialBT.hasClient();
  if (cliente && !clienteAnterior) {
    SerialBT.print("I," NOMBRE_BLUETOOTH "," VERSION_FIRMWARE "\n");
    hapticaReproducir(4, 200);
  }
  clienteAnterior = cliente;

  if ((ahoraMs - tEnvio) >= PERIODO_ENVIO_MS) {
    tEnvio = ahoraMs;
    enviarEstado();
  }
  if ((ahoraMs - tBateria) >= 200) {
    tBateria = ahoraMs;
    bateriaActualizar();
  }
  ledActualizar();
}
