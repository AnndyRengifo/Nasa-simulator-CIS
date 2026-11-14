# SOL-3: Base Aurora — Entrega final de las cinemáticas 3D

Unity 6000.6.9f1 · URP 17.6 · Timeline 6.6 · Cinemachine 6.6 (API Cinemachine 3) · TextMeshPro (uGUI 2.6) · Android (IL2CPP, ARM64).
El juego principal sigue siendo 2D; las cinemáticas son escenas 3D independientes. Detalle técnico en `README_Cinematicas.md` e informes `FaseA…FaseE_Informe.md`.

> **Calidad visual:** personajes y escenarios son **provisionales**, construidos con primitivas y mallas procedurales por falta de modelos. La estructura permite sustituirlos (§9) y las cinemáticas se actualizarán solas.

## 1. Cinemáticas desarrolladas

| Id | Duración | Planos | Contenido |
|---|---|---|---|
| `intro` | ≈76 s | 8 | Marte desde el espacio → descenso → exterior de la base → interior → astronauta → aparición de AURA → "Por ahora." |
| `sol1_night` | ≈33 s | 5 | Atardecer, paneles plegándose, baterías en uso, aviso de AURA, la base de noche |
| `solar_alert` | ≈41 s | 6 | El Sol y su actividad, alerta en el control, AURA analiza, refugio, decisiones |
| `sol3_start` | ≈40 s | 6 | Amanecer, recorrido por la base, sistemas funcionando, preparación, objetivos finales |
| `ending_success` | ≈52 s | 6 | Control, informe, transmisión, confirmación de la Tierra, astronauta y AURA, exterior |
| `ending_failure` | ≈45 s | 6 | Alerta, fallo de sistemas, luces de emergencia, AURA, interrupción, base en estado crítico |
| `ending_partial` | ≈39 s | 5 | Tripulación a salvo, experimentos incompletos, sistemas funcionando, AURA, la colonia |

Todas con diálogos literales del guion en subtítulos, omisión, fundidos y vuelta al juego.

## 2. Escenas creadas (`Assets/Cinematics3D/Scenes`)

`CIN_intro`, `CIN_sol1_night`, `CIN_solar_alert`, `CIN_sol3_start`, `CIN_ending_success`, `CIN_ending_failure`, `CIN_ending_partial` (en la build) · `CIN_Template` (plantilla para nuevas cinemáticas) · `CIN_FaseB_Galeria` (catálogo visual de recursos) · `Tests/SOL3_CinematicTestHarness` (banco de pruebas que simula el juego 2D).

## 3. Timelines (`Assets/Cinematics3D/Timelines`)

`TL_<id>.playable` para cada cinemática, más `TL_Template` y `TL_FaseB_Galeria`. Grupos: **CÁMARAS** (pista `Brain` + pistas de dolly `CM_<id>_*`), **ACTORES** (`Astronaut`, `AURA`…), **ESCENARIO** (`SolarArray`, `BatteryBank`), **ILUMINACIÓN Y AMBIENTE** (`SunRig`, `Env_*`, luces de emergencia, efectos).

## 4. Modelos y recursos

* Personajes: `Prefabs/Characters/PF_Astronaut` (11 848 triángulos tras la optimización de la Fase F, 8 animaciones) y `PF_AURA` (4 804 triángulos, 4 animaciones).
* Escenarios: `PF_Set_MarsSurface` (terreno + rocas + `PF_BaseAurora_Exterior`), `PF_Room_ControlCenter`, `PF_Room_PowerRoom`, `PF_Room_Shelter`, `PF_Room_EarthControl`, `PF_Space_Mars`, `PF_Space_Earth`, `PF_Space_Sun`.
* Props anidados (`Prefabs/Props`), efectos (`Prefabs/FX`: polvo, erupción solar, chispas, transmisión), materiales de paleta (`Materials/M_*`), cielos (`Materials/Sky`), texturas procedurales (`Textures/Generated`), mallas procedurales (`Models/Procedural`).
* Diálogos: `Subtitles/SUB_<id>_es.asset`; personajes que hablan `Config/Speakers/SPK_AURA`, `SPK_ControlTierra`.
* Audio: pistas y fuentes `Music`/`SFX` y campos de voz preparados; **sin música ni voces grabadas todavía**.

## 5. Scripts

**Runtime (`SOL3.Cinematics`)**: `CinematicManager` (reproducción), `ICinematicService`, `ICinematicGameBridge`, `SimpleCinematicGameBridge`, `CinematicRequester`, `CinematicCatalog`, `CinematicDefinition`, `SceneReference`, `CinematicIds`, `CinematicSceneRoot` (contrato de escena), `CinematicTimelineBinder`, `CinematicBindingTarget`, `CinematicBindingKeys`, `CinematicLayers`, `SubtitleSheet`, `CinematicSpeaker`, `CinematicSubtitlePresenter`, `CinematicOverlay`, `CinematicEnvironmentProfile`, `CinematicAudioSettings`, `CinematicResult`, `Testing/CinematicTestHarness`.

**Editor (`SOL3.Cinematics.Editor`)**: menú `SOL-3 > Cinemáticas` (fases A–F, validación, plantilla, galería, banco de pruebas, builds Android), constructores de escenas y recursos, validador, informe de rendimiento, ejecución de pruebas.

**Pruebas**: `Tests/Editor/CinematicContentTests` (EditMode) y `Tests/Runtime/CinematicManagerTests` (PlayMode).

## 6. Configuración de Cinemachine

Cámara física `CinematicCamera` por escena: `CinemachineBrain` (LateUpdate, **ignora timeScale**, mezcla por defecto EaseInOut 1 s), renderer 3D del URP Asset, *Culling Mask* = `Cinematic3D`, FXAA, post-procesado. Cámaras virtuales `CM_<id>_NN_<plano>`: `CinemachineCamera` + `CinemachineRotationComposer` (encuadre), `CinemachineBasicMultiChannelPerlin` (cámara en mano, perfiles `Cameras/Noise/NP_*`) y, en los travellings, `CinemachineSplineDolly` sobre `Dolly_<id>_*` animado por el Timeline (posición normalizada 0→1 con aceleración suave).

## 7. Configuración de Timeline

`PlayableDirector` en `[CINEMATIC_ROOT]`: sin *Play On Awake* (lo controla el gestor), **tiempo no escalado** (funciona con el juego en pausa), extrapolación *Hold* (sin saltos al final). Cada plano es un clip de la pista `Brain`; solapar dos clips crea un fundido de cámara. El nombre de cada pista es la clave de enlace de su objeto (`CinematicBindingTarget`), lo que permite sustituir el Timeline sin abrir la escena.

## 8. Cómo reproducir cada secuencia

* **En el Editor, una sola**: abre `CIN_<id>` y pulsa Play (vista previa con subtítulos y bandas).
* **Como en el juego**: abre `Tests/SOL3_CinematicTestHarness`, Play, y pulsa el botón de la cinemática.
* **Desde el juego 2D**: `CinematicManager.Instance.Play(CinematicIds.Sol1Night, resultado => …);` (la lógica del juego decide cuándo; ver `FaseE_Informe.md`).
* **En Android**: menú *Android · Build de prueba del sistema completo* → `adb install -r Builds/Android/SOL3_Cinematics_System.apk`.

## 9. Cambiar personajes y escenarios (Nivel 1)

| Quiero cambiar | Dónde |
|---|---|
| Astronauta / AURA | `Prefabs/Characters/PF_Astronaut` / `PF_AURA` → sustituir el hijo `Visual/Model` (conservar nombres de huesos para reutilizar animaciones, o usar clips Humanoid) |
| Base exterior, terreno, rocas | `Prefabs/Environment/PF_BaseAurora_Exterior`, `PF_Mars_Terrain`, `PF_Mars_RockField` |
| Salas | `PF_Room_*` (conservar `Lights_Normal`, `EmergencyLights`, `BatteryBank`, `Anchor_*`) |
| Planetas y Sol | `PF_Space_*` |
| Un prop en todas las escenas | `Prefabs/Props/PF_Prop_*` |
| Colores, acabados, texturas, cielos | `Materials/M_*`, `Textures/Generated/*.png` (mismo nombre), `Materials/Sky/M_Sky_*` |
| Solo en una cinemática | `Config/Definitions/CinematicDef_<id>` → *Cast Overrides* |

## 10. Modificar cámaras y animaciones (Nivel 2)

Abre `CIN_<id>`, selecciona `[CINEMATIC_ROOT]` y *Window > Sequencing > Timeline*:
* **Duración y orden de planos / transiciones**: mover, recortar o solapar clips de la pista `Brain`.
* **Encuadre y lente**: objeto `CM_<id>_*` (FOV, composición, ruido). **Recorridos**: knots de `Dolly_<id>_*`.
* **Animaciones**: clips de las pistas de actores y props; clips en `Animations/` (`Astronaut/`, `AURA/`, `Props/`, `<id>/`).
* **Iluminación y ambiente**: pista `SunRig` (clip `AC_<id>_SunMotion`), pistas `Env_*` (objetos `CinematicEnvironmentProfile` de `Lighting`), perfil `Rendering/VP_Cinematic_Default`.
* **Textos y tiempos de diálogo**: `Subtitles/SUB_<id>_es` (sin tocar el Timeline).
* **Fundidos y omisión**: `CinematicDef_<id>`.

## 11. Sustituir una cinemática completa (Nivel 3) — ejemplo `intro`

1. Duplica `CIN_Template` (o `CIN_intro`) como `CIN_intro_v2` y crea la secuencia; mantén un `CinematicSceneRoot` con id `intro`.
2. Pulsa *Añadir a la build* en el selector de escena o añádela a la lista de escenas.
3. En `Config/Definitions/CinematicDef_intro` asigna `CIN_intro_v2` en **Scene** (y su hoja de subtítulos).
4. El juego sigue llamando a `Play("intro")`: **no se modifica `CinematicManager` ni el juego**. Volver atrás = reasignar la escena anterior.

Variante: solo un Timeline nuevo (`TL_intro_v2`) con los mismos nombres de pista → asígnalo en *Timeline Override*.

### Tabla de sustitución

| Cinemática | Escena | Timeline | Qué puedo cambiar |
|---|---|---|---|
| Intro | `CIN_intro` | `TL_intro` | Marte, base, astronauta, AURA y cámaras |
| Primera noche | `CIN_sol1_night` | `TL_sol1_night` | Exterior, iluminación (sol y noche), paneles y baterías |
| Alerta solar | `CIN_solar_alert` | `TL_solar_alert` | Actividad solar, alerta, AURA y refugio |
| Tercer sol | `CIN_sol3_start` | `TL_sol3_start` | Amanecer, recorrido, sistemas y presentación |
| Victoria | `CIN_ending_success` | `TL_ending_success` | Informe, transmisión, Control Tierra y cierre |
| Derrota | `CIN_ending_failure` | `TL_ending_failure` | Fallos, alertas, emergencia y base crítica |
| Final parcial | `CIN_ending_partial` | `TL_ending_partial` | Estado de la base, experimentos y cierre |

## 12. Pruebas realizadas y resultados

**41 pruebas automáticas superadas de 41** (30 de contenido + 11 de integración, 0 fallidas). Detalle en `FaseF_Pruebas.md`.
