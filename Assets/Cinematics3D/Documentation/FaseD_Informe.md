# Fase D — Cinemáticas de final · Informe (14/11/2026)

Menú: **SOL-3 > Cinemáticas > Fase D · Generar finales**. Regenera las tres escenas cada vez (no toca los recursos de la Fase B ni las cinemáticas de la Fase C). Cada final queda registrado en su `CinematicDef_<id>` y en la build.

**Los finales son solo presentación**: la lógica del juego decide si hubo victoria, derrota o final parcial y después pide la cinemática. Reproducir un final no cambia el resultado de la partida.

Personajes y escenarios siguen siendo los **provisionales** de la Fase B.

## `ending_success` — `CIN_ending_success` / `TL_ending_success` (≈52 s, 6 planos)

| Tiempo | Plano | Qué ocurre |
|---|---|---|
| 0–7 s | `01_Control` (dolly) | Centro de control; el astronauta trabaja en la consola de comunicaciones |
| 7–14 s | `02_Report` | Preparación del informe científico (pantalla de misión) |
| 14–22 s | `03_Transmission` (dolly) | La antena emite pulsos de transmisión hacia la Tierra. AURA: "Transmisión completada.", "Nuestros resultados han sido enviados a la Tierra." |
| 22–30,5 s | `04_EarthControl` (dolly) | Confirmación en Control Tierra: "Base Aurora, hemos recibido su informe.", "Excelente trabajo, tripulación." |
| 30,5–42 s | `05_Duo` | El astronauta celebra; AURA: "Superamos la misión…", "Cada recurso que protegimos…" |
| 42–52 s | `06_Outro` (dolly) | Vista exterior de Base Aurora con luz dorada, alejándose |

## `ending_failure` — `CIN_ending_failure` (≈45 s, 6 planos)

Alerta en el centro de control (luces de emergencia, pantalla roja, AURA en modo alerta, el astronauta preocupado) → sala de máquinas con baterías agotándose y chispas → paneles solares plegados y producción detenida con tormenta de polvo → AURA informa → la tripulación se protege en el refugio → imagen final de la base en estado crítico (cielo rojizo, balizas y luz de alarma). Cinco líneas de AURA, incluida "En el espacio, incluso una decisión pequeña…".

## `ending_partial` — `CIN_ending_partial` (≈39 s, 5 planos)

El astronauta en la base ("La tripulación está a salvo.") → experimentos incompletos (pantalla de resultados pendientes y muestras sin procesar) → sistemas esenciales funcionando (baterías cargándose) → AURA presenta el resultado ("Pero aprendimos algo fundamental…") → vista de la colonia al atardecer.

## Dónde cambiar cada cosa

Igual que en la Fase C: textos en `Subtitles/SUB_<id>_es.asset`; planos y tiempos en `TL_<id>` (pista `Brain`); encuadres en `CM_<id>_*` y recorridos en `Dolly_<id>_*`; acciones en las pistas de actores y props; luz y ambiente en `SunRig` y `Env_*`.

Cada pista del Timeline tiene como nombre la clave de enlace de su objeto (por ejemplo `PowerEmergencyLights`, `Sparks`, `TransmissionPulse`), así que un Timeline nuevo con esos nombres se enlaza solo (sustitución de Nivel 3).

## Verificación hecha

* Compilación offline con el Roslyn de Unity 6.6 (Editor y perfil Android IL2CPP): **sin errores ni avisos**.

## Pendiente de verificar (requiere el Editor)

* Generar las escenas en Unity y revisarlas en Play; posibles ajustes finos de encuadre y tiempos.
