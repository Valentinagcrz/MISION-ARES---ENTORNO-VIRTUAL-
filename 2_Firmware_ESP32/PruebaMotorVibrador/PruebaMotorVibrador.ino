// Prueba del módulo "Vibration Motor" con ESP32
// Conexión: IN -> GPIO23   VCC -> 3V3   GND -> GND
// Abre el Monitor Serie a 115200 para ver qué está haciendo.

const int PIN_MOTOR = 23;

void vibrar(int fuerza, int ms) {        // fuerza 0..255
  ledcWrite(PIN_MOTOR, fuerza);
  delay(ms);
  ledcWrite(PIN_MOTOR, 0);
}

void setup() {
  Serial.begin(115200);
  ledcAttach(PIN_MOTOR, 5000, 8);        // PWM 5 kHz, 8 bits (core ESP32 3.x)
  ledcWrite(PIN_MOTOR, 0);
}

void loop() {
  Serial.println("1) Encendido total 1 s");
  vibrar(255, 1000);
  delay(1000);

  Serial.println("2) Intensidades: 120, 180, 255");
  vibrar(120, 600); delay(400);
  vibrar(180, 600); delay(400);
  vibrar(255, 600); delay(1000);

  Serial.println("3) Patron OBJETO: tres toques suaves");
  for (int i = 0; i < 3; i++) { vibrar(170, 60); delay(60); }
  delay(1000);

  Serial.println("4) Patron PARED: zumbido fuerte y largo");
  vibrar(255, 350); delay(70); vibrar(255, 150);
  delay(2000);
}
