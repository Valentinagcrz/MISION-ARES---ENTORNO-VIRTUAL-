// ============================================================================
//  Carcasa del CONTROL HÁPTICO INALÁMBRICO - Teleoperación RV (UMNG)
//  Diseño paramétrico en OpenSCAD (https://openscad.org, gratis).
//
//  Piezas:  pieza = "base"      -> cuerpo inferior (protoboard, batería, regulador, vibrador)
//           pieza = "tapa"      -> tapa superior con el joystick
//           pieza = "ensamble"  -> vista de ambas piezas juntas (no imprimir)
//
//  Para exportar STL:  F6 (Render)  y luego  F7 (Export STL)
//  Impresión sugerida: PLA o PETG, capa 0.2 mm, 3 perímetros, 20 % relleno,
//  base boca arriba (sin soportes), tapa boca abajo (cara superior sobre la cama).
//
//  Distribución (vista desde arriba, +X = frente del control):
//
//     atrás                                              frente
//    +------------------------------------+-----------------------+
//    |                     [vibrador en la pared]                 |
//    |                                    |  REGULADOR LM2596S    |
//    |   PROTOBOARD 80 x 55               |  55 x 23              |
//    |   (ESP32 + MPU6050)                |-----------------------|
//    |                                    |  BATERÍA 12 V 55 x 25 |
//    +------------------------------------+-----------------------+
//     USB ESP32 por atrás                  joystick encima (tapa)
//
//  Medidas de los componentes (dadas por el equipo; ajusta si cambian):
//    Protoboard con ESP32 e IMU .... 80 x 55 x 30 mm (alto con componentes)
//    Batería 12 V ................... 55 x 25 x 20 mm
//    Regulador LM2596S .............. 55 x 23 x 20 mm
//    Módulo "Vibration Motor" ....... ~22 x 20 mm (conectado con jumpers)
//    Joystick HW-504 ................ PCB 34 x 26 mm (conectado con jumpers)
//    Tornillos: 8 x M3 x 15 mm (4 para cerrar la tapa y 4 para el joystick)
// ============================================================================

pieza = "ensamble";          // "base", "tapa" o "ensamble"

// ---------------- Componentes (mm) ----------------
proto  = [80, 55, 30];       // protoboard: largo (X), ancho (Y), alto con componentes
bat    = [55, 25, 20];       // batería 12 V
reg    = [55, 23, 20];       // regulador LM2596S
holgc  = 1.0;                // holgura alrededor de cada componente

// ---------------- Dimensiones generales ----------------
e    = 2.4;                  // espesor de pared
piso = 2.4;                  // espesor del piso / techo
L    = 160;                  // largo exterior
W    = 77;                   // ancho exterior
Hb   = 30;                   // alto de la base
Ht   = 15;                   // alto de la tapa  (interior útil = Hb + Ht - 2*piso ≈ 40 mm)
R    = 12;                   // radio de esquinas en planta
holg = 0.25;                 // holgura del encaje tapa-base
$fn  = 64;

// Posiciones (esquina inferior-izquierda de cada componente, coordenadas exteriores)
p_proto = [e + 8.5,  e + 3];                                 // protoboard atrás
p_bat   = [e + 91.5, e + 3];                                 // batería al frente, lado izquierdo (Y bajo)
p_reg   = [e + 91.5, e + 3 + bat[1] + holgc + 3];            // regulador al lado de la batería

// Tornillos de la tapa
d_piloto = 2.7;  d_paso = 3.4;  d_cabeza = 6.2;  d_torre = 7.5;
prof_cabeza = 8;             // qué tan hundida queda la cabeza del tornillo en la tapa (M3 x 15 -> ~8 mm dentro de la base)
pos_torres = [[7, 7], [L-7, 7], [7, W-7], [L-7, W-7]];

// ---------------- Joystick HW-504 (MIDE EL TUYO) ----------------
joy_centro      = [p_bat[0] + bat[0]/2, W/2];   // sobre la zona de batería/regulador, bajo el pulgar
joy_agujeros    = [27, 19.5];   // distancia entre centros de los agujeros de su PCB
joy_off_palanca = 1.5;          // palanca corrida del centro de la PCB hacia el frente (+X)
joy_h_cuerpo    = 12;           // de la cara superior de la PCB al tope del cuerpo negro
d_abertura      = 29;           // hueco para la cúpula
d_poste_joy     = 5.5;
d_piloto_joy    = 2.6;          // tornillos M3 x 15 (autorroscantes en el plástico)

// ---------------- Módulo de vibración (MIDE EL TUYO) ----------------
// Va PARADO contra la pared lateral (+Y), con el motor tocando la pared para que
// la vibración llegue a la mano. Se desliza en dos ranuras y se sujeta con silicona.
vib_pcb   = [22, 20];           // largo x alto de la placa
vib_x     = e + 30;             // posición a lo largo de la pared (inicio)
vib_motor = 3.5;                // espesor del motor (distancia placa-pared)
vib_pcb_e = 1.8;                // espesor de la ranura para la placa

// ---------------- Utilidades ----------------
module rect_redondeado(l, w, r, h) {
    hull() for (x = [r, l-r], y = [r, w-r]) translate([x, y, 0]) cylinder(r = r, h = h);
}
module cuerpo_base() {
    hull() {
        translate([3, 3, 0]) rect_redondeado(L-6, W-6, R-3, 1);
        translate([0, 0, 5]) rect_redondeado(L, W, R, Hb-5);
    }
}
module cuerpo_tapa() {
    hull() {
        rect_redondeado(L, W, R, Ht-4);
        translate([2.5, 2.5, 0]) rect_redondeado(L-5, W-5, R-2.5, Ht);
    }
}
// Marco bajo que rodea un componente (evita que se deslice)
module marco(p, t, alto = 5) {
    translate([p[0] - holgc/2 - 1.5, p[1] - holgc/2 - 1.5, piso - 0.5]) difference() {
        cube([t[0] + holgc + 3, t[1] + holgc + 3, alto + 0.5]);
        translate([1.5, 1.5, -1]) cube([t[0] + holgc, t[1] + holgc, alto + 3]);
        // aberturas en los lados largos para pasar cables y sacar el componente
        translate([t[0]/2 - 6, -1, 1.5]) cube([15, t[1] + holgc + 5, alto + 3]);
    }
}
module ranuras_dedos() {
    for (x = [25 : 14 : 80]) for (y = [-1, W+1])
        translate([x, y, 15]) scale([1, 0.5, 1]) cylinder(d = 9, h = Hb, center = true);
}

// ============================================================================
//  BASE
// ============================================================================
module base() {
    difference() {
        union() {
            difference() {
                cuerpo_base();
                translate([e, e, piso]) rect_redondeado(L-2*e, W-2*e, R-e, Hb);
                ranuras_dedos();
            }
            intersection() {
                cuerpo_base();
                for (p = pos_torres) translate([p[0], p[1], 0]) cylinder(d = d_torre, h = Hb);
            }
            marco(p_proto, proto, 5);
            marco(p_bat, bat, 6);
            marco(p_reg, reg, 6);
            // ranuras para el módulo de vibración contra la pared +Y
            for (dx = [-3, vib_pcb[0] + 0.6]) translate([vib_x + dx, W - e - vib_motor - vib_pcb_e - 2.5, piso - 0.5])
                difference() {
                    cube([3, vib_motor + vib_pcb_e + 2.5 + 0.5, vib_pcb[1] + 0.5]);
                    translate([dx < 0 ? 1.5 : -0.1, 2.5, 2]) cube([1.6, vib_pcb_e, vib_pcb[1] + 2]);
                }
            // tope inferior para la placa del vibrador
            translate([vib_x - 3, W - e - vib_motor - vib_pcb_e - 2.5, piso - 0.5]) cube([vib_pcb[0] + 6.6, 2.5, 2.5]);
        }
        for (p = pos_torres) translate([p[0], p[1], 4]) cylinder(d = d_piloto, h = Hb);
        // USB del ESP32 (atrás, centrado con la protoboard): orienta el ESP32 con el USB hacia atrás
        translate([-1, p_proto[1] + proto[1]/2 - 18, piso + 10]) cube([e + 2, 36, Hb]);
        // interruptor (pared del frente, lado +Y)
        translate([L - e - 1, W - 22, 12]) cube([e + 2, 12.5, 6.5]);
        // ventilación bajo el regulador
        for (i = [0:5]) translate([p_reg[0] + 8 + i*7, p_reg[1] + 5, -1]) cube([2.5, reg[1] - 10, piso + 2]);
        translate([L/2, W/2, 0.5]) rotate([180, 0, 0]) linear_extrude(1)
            text("UMNG - Mecatronica - RV", size = 5, halign = "center", valign = "center");
    }
}

// ============================================================================
//  TAPA (modelada boca arriba; se imprime volteada)
// ============================================================================
joy_pcb_c = [joy_centro[0] - joy_off_palanca, joy_centro[1]];
z_techo   = Ht - piso;
h_poste   = joy_h_cuerpo + 0.8;

module tapa() {
    difference() {
        union() {
            difference() {
                cuerpo_tapa();
                translate([e, e, -1]) rect_redondeado(L-2*e, W-2*e, R-e, Ht-piso+1);
            }
            difference() {
                translate([e+holg, e+holg, -3]) rect_redondeado(L-2*(e+holg), W-2*(e+holg), R-e-holg, 3.2);
                translate([e+holg+1.4, e+holg+1.4, -4]) rect_redondeado(L-2*(e+holg+1.4), W-2*(e+holg+1.4), R-e-holg-1.4, 6);
            }
            intersection() {
                cuerpo_tapa();
                for (p = pos_torres) translate([p[0], p[1], 0]) cylinder(d = d_torre, h = Ht);
            }
            for (sx = [-1, 1], sy = [-1, 1])
                translate([joy_pcb_c[0] + sx*joy_agujeros[0]/2, joy_pcb_c[1] + sy*joy_agujeros[1]/2, z_techo - h_poste])
                    cylinder(d = d_poste_joy, h = h_poste + 0.5);
            translate([joy_centro[0], joy_centro[1], Ht - 0.01]) difference() {
                cylinder(d1 = d_abertura + 9, d2 = d_abertura + 5, h = 2);
                translate([0, 0, -1]) cylinder(d = d_abertura, h = 4);
            }
        }
        for (p = pos_torres) translate([p[0], p[1], -5]) {
            cylinder(d = d_paso, h = Ht+10);
            translate([0, 0, Ht+5-prof_cabeza]) cylinder(d = d_cabeza, h = 10);
        }
        translate([joy_centro[0], joy_centro[1], z_techo - 1]) cylinder(d = d_abertura, h = Ht + 10);
        translate([joy_centro[0], joy_centro[1], Ht + 0.8]) cylinder(d1 = d_abertura, d2 = d_abertura + 3, h = 1.25);
        for (sx = [-1, 1], sy = [-1, 1])
            translate([joy_pcb_c[0] + sx*joy_agujeros[0]/2, joy_pcb_c[1] + sy*joy_agujeros[1]/2, z_techo - h_poste - 1])
                cylinder(d = d_piloto_joy, h = h_poste + 1.5);
        // textos
        translate([joy_centro[0] + d_abertura/2 + 9, joy_centro[1], Ht-0.6]) rotate([0, 0, 90]) linear_extrude(1)
            text("ADELANTE", size = 3, halign = "center", valign = "center");
        translate([joy_centro[0] - d_abertura/2 - 7, joy_centro[1], Ht-0.6]) rotate([0, 0, 90]) linear_extrude(1)
            text("ATRAS", size = 3, halign = "center", valign = "center");
        translate([joy_centro[0], joy_centro[1] - d_abertura/2 - 8, Ht-0.6]) linear_extrude(1)
            text("PRESIONAR = AGARRE", size = 2.8, halign = "center", valign = "center");
        translate([48, W/2, Ht-0.6]) linear_extrude(1)
            text("CONTROL HAPTICO RV", size = 4.5, halign = "center", valign = "center");
        translate([48, W/2 - 9, Ht-0.6]) linear_extrude(1)
            text("<- atras    frente ->", size = 3.2, halign = "center", valign = "center");
    }
}

// ============================================================================
if (pieza == "base") base();
else if (pieza == "tapa") tapa();
else {
    color("SteelBlue") base();
    color("Orange", 0.9) translate([0, 0, Hb + 0.5]) tapa();
}
