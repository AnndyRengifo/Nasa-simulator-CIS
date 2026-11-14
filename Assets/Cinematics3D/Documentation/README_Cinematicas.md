# SOL-3: Base Aurora — Sistema de cinemáticas 3D

Documento vivo. Se completa al cerrar cada fase (A → F). Estado actual: **Fase F — Pruebas y entrega** (ver `Entrega_Final.md` y `FaseF_Pruebas.md`).

---

## 1. Principios

1. **El juego principal sigue siendo 2D.** Las cinemáticas son escenas 3D independientes que se cargan de forma aditiva encima del juego 2D (que queda pausado en memoria, con su estado intacto) y se descargan al terminar.
2. **Identificadores estables.** `intro`, `sol1_night`, `solar_alert`, `sol3_start`, `ending_success`, `ending_failure`, `ending_partial` (`CinematicIds.cs`). Nunca cambian.
3. **Configuración central.** `Config/CinematicCatalog.asset` relaciona cada identificador con su definición (`CinematicDef_<id>.asset`): escena, Timeline, subtítulos, omisión, transiciones, final. Reasignar una cinemática no requiere código.
4. **Contrato de escena.** Cada escena cinemática tiene un único `CinematicSceneRoot`. El sistema de reproducción solo habla con él: la escena puede rehacerse entera sin tocar la lógica.
5. **Enlaces por convención.** Nombre de pista del Timeline = clave de un `CinematicBindingTarget`. Un Timeline nuevo se enlaza solo.
6. **Aislamiento visual.** Todo lo cinemático vive en la capa `Cinematic3D` y se dibuja con un **Universal Renderer 3D** propio. El renderer por defecto del proyecto sigue siendo el **2D** del juego.
7. **Presentación, no lógica.** Reproducir una cinemática nunca cambia el resultado de la partida. La lógica del juego decide qué final corresponde y después lo pide.
8. **Nada bloquea la partida.** Si falta una escena, un Timeline o un recurso, la cinemática se da por fallida (`CinematicEndReason.Failed`) y el juego continúa.

## 2. Versiones verificadas (Fase A, 14/11/2026)

| Elemento | Versión | Nota |
|---|---|---|
| Unity Editor | 6000.6.9f1 (Unity 6.6) | Plantilla 2D multiplataforma. No se cambia la versión. |
| Universal RP | 17.6.0 | Renderer por defecto: `Renderer2D` (juego). Se añade `CinematicUniversalRenderer` (3D). |
| Timeline | 6.6.0 (builtin) | Ya instalado. |
| Cinemachine | 6.6.0 (builtin) | API de Cinemachine 3 (`Unity.Cinemachine`). Añadido al manifiesto en la Fase A. |
| Splines | 2.9.2 | Dependencia de Cinemachine (dollies). |
| uGUI + TextMesh Pro | 2.6.0 | TextMesh Pro viene dentro de uGUI. |
| Input System | 1.20.1 | *Active Input Handling* = solo Input System → la UI usará `InputSystemUIInputModule`. |
| Android | Build Support instalado | IL2CPP · ARM64 · API mínima 26 · Linear · APIs gráficas automáticas. |

## 3. Arquitectura

```
Juego 2D (lógica de partida)
  │  decide QUÉ cinemática corresponde (nueva partida, noche, evento solar, sol 3, victoria/derrota/parcial)
  ▼
ICinematicService.Play("sol1_night", onFinished)        ← CinematicManager (Fase E)
  │ 1. ICinematicGameBridge.OnCinematicStarting  → el juego pausa el 2D, guarda estado, apaga su cámara/AudioListener
  │ 2. CinematicCatalog.TryGet(id)               → CinematicDefinition (escena, Timeline, subtítulos…)
  │ 3. Fundido a negro + LoadSceneAsync(CIN_<id>, Additive) + SetActiveScene (iluminación propia)
  ▼
CinematicSceneRoot  (contrato de escena)
  │ Prepare(timelineOverride, castOverrides)     → capa, sustituciones de modelo, enlaces por convención
  │ Play()                                       → PlayableDirector + TL_<id> (Cinemachine, animación, luces, audio)
  │ Skip() / StopPlayback()
  │ Finished(reason)
  ▼
CinematicManager
  │ 4. Fundido + UnloadSceneAsync + Resources.UnloadUnusedAssets
  │ 5. ICinematicGameBridge.OnCinematicFinished(result) → el juego restaura su estado y reanuda
  ▼
Juego 2D
```

Scripts de la Fase A:

| Script | Ensamblado | Función |
|---|---|---|
| `CinematicIds` | Runtime | Los 7 identificadores estables. |
| `CinematicCatalog` | Runtime | Configuración central + ajustes globales. |
| `CinematicDefinition` | Runtime | Configuración de una cinemática (escena, Timeline opcional, duración, subtítulos, omisión, fundidos, final, escena de retorno, sustitución de modelos). |
| `SceneReference` | Runtime | Referencia a escena robusta (ruta + GUID) con selector en el Inspector. |
| `SubtitleSheet`, `CinematicSpeaker` | Runtime | Diálogos separados de animación y cámaras: texto, personaje, inicio, fin, idioma, voz y volumen. |
| `CinematicSceneRoot` | Runtime | Contrato de escena (preparar, reproducir, omitir, detener, fin). Vista previa al pulsar Play en la escena sola. |
| `CinematicBindingTarget`, `CinematicBindingKeys`, `CinematicTimelineBinder` | Runtime | Enlaces Timeline ↔ escena por convención de nombres. |
| `CinematicLayers` | Runtime | Capa `Cinematic3D` (siempre por nombre). |
| `ICinematicService`, `ICinematicGameBridge`, `CinematicResult` | Runtime | API para el juego 2D (la implementación llega en la Fase E). |
| `CinematicProjectSetup`, `CinematicTemplateBuilder` | Editor | Asistente idempotente de la Fase A y escena de plantilla. |
| `CinematicValidator`, `CinematicReport` | Editor | Validación (paquetes, render, capa, catálogo, Android). |
| `SceneReferenceDrawer`, `CinematicIdDrawer` | Editor | Inspector amigable. |
| `CinematicLayerAutoAssign` | Editor | Asigna la capa `Cinematic3D` al guardar escenas cinemáticas. |
| `CinematicAndroidSmokeBuild` | Editor | APK de prueba con solo la plantilla 3D. |

## 4. Estructura de carpetas

```
Assets/Cinematics3D/
├── Scenes/          CIN_<id>.unity (una por cinemática) · CIN_Template.unity
├── Timelines/       TL_<id>.playable · TL_Template.playable
├── Animations/      <id>/AC_<id>_<Descripcion>.anim · Template/
├── Models/          Astronaut/ · AURA/ · BaseAurora/ · Mars/ · EarthControl/ · Props/   (FBX/mallas)
├── Materials/       M_<Nombre>.mat · Placeholders/ (M_PH_* provisionales)
├── Textures/        T_<Nombre>_<Mapa>.png
├── Audio/           Music/ · SFX/ · Voice/<idioma>/ · Mixer/
├── Subtitles/       SUB_<id>_<idioma>.asset
├── Cameras/         Noise/NP_*.asset (perfiles de ruido editables) · ajustes de mezcla
├── Prefabs/         Characters/PF_Astronaut, PF_AURA · Environment/ · Props/ · System/
├── Config/          CinematicCatalog.asset · Definitions/CinematicDef_<id>.asset · Speakers/SPK_*.asset
├── Rendering/       CinematicUniversalRenderer.asset · VP_*.asset (post-procesado) · LS_Cinematic_Realtime.lighting
├── Scripts/         Runtime/ (SOL3.Cinematics) · Editor/ (SOL3.Cinematics.Editor)
└── Documentation/   este documento · informes por fase
```

Nada de esta carpeta se mezcla con las escenas 2D (`Assets/Scenes`).

## 5. Convenciones de nombres

| Elemento | Patrón | Ejemplo |
|---|---|---|
| Escena | `CIN_<id>.unity` | `CIN_intro.unity` |
| Timeline | `TL_<id>.playable` | `TL_solar_alert.playable` |
| Definición | `CinematicDef_<id>.asset` | `CinematicDef_ending_success.asset` |
| Subtítulos | `SUB_<id>_<idioma>.asset` | `SUB_sol1_night_es.asset` |
| Cámara virtual | `CM_<id>_<NN>_<Plano>` | `CM_intro_03_BaseExterior` |
| Recorrido de dolly | `Dolly_<id>_<NN>_<Plano>` | `Dolly_intro_02_Descent` |
| Animación | `AC_<id>_<Accion>.anim` | `AC_sol3_start_AstronautSuitUp.anim` |
| Prefab | `PF_<Nombre>.prefab` | `PF_Astronaut.prefab` |
| Material | `M_<Nombre>.mat` (`M_PH_` = provisional) | `M_BaseAurora_Panel.mat` |
| Personaje que habla | `SPK_<Nombre>.asset` | `SPK_AURA.asset` |
| Perfil de post-procesado | `VP_<id o uso>.asset` | `VP_ending_failure.asset` |

## 6. Jerarquía estándar de una escena cinemática

```
[CINEMATIC_ROOT]       CinematicSceneRoot + PlayableDirector (TL_<id>)
Cameras/
  CinematicCamera      Camera (renderer 3D, Culling Mask = Cinematic3D) + CinemachineBrain + AudioListener   [clave "Brain"]
  CM_<id>_NN_<Plano>   CinemachineCamera (+ SplineDolly / RotationComposer / ruido)
  Dolly_<id>_NN_...    SplineContainer
Lighting/              Sun, luces de interior/alerta, GlobalVolume (VP_*)
Environment/           Marte, exterior/interior de la base, props
Actors/                Astronaut, AURA, ...  (raíz animada + hijo "Visual" con el modelo reemplazable)
Audio/                 Music, Ambience, SFX  (AudioSource enlazados a pistas de audio)
```

`CIN_Template.unity` implementa exactamente esta estructura y es el punto de partida de cada cinemática.

## 7. Enlaces por convención

| Pista del Timeline (nombre) | Tipo | Se enlaza con |
|---|---|---|
| `Brain` | Cinemachine Track | `CinemachineBrain` de `CinematicCamera` |
| `Music` / `Ambience` / `SFX` | Audio Track | `AudioSource` del mismo nombre |
| `Astronaut` / `AURA` / `EarthControl` | Animation Track | `Animator` del actor |
| `<NombreDeObjeto>` | Animation / Activation | `CinematicBindingTarget` con esa clave |

* Clips de la pista `Brain`: su nombre = nombre del GameObject de la `CinemachineCamera`.
* Clips de Control Track: su nombre = nombre del GameObject controlado.
* Los enlaces hechos a mano en la escena siempre se respetan; la convención solo completa los que faltan.

## 8. Render, capa e iluminación

* El URP Asset (`Assets/Settings/UniversalRP.asset`) tiene ahora dos renderers: **0 = Renderer2D** (por defecto, juego) y **1 = CinematicUniversalRenderer** (Forward, solo cámaras cinemáticas). El índice se guarda en el catálogo.
* Capa `Cinematic3D`: la cámara cinemática solo la dibuja; **la cámara 2D del juego debe excluirla** de su *Culling Mask*.
* Iluminación 100 % en tiempo real (`LS_Cinematic_Realtime`): sin lightmaps que hornear ni cargar en Android. Ambiente *Trilight* y niebla por escena.
* Post-procesado (`VP_Cinematic_Default`): Tonemapping Neutral (respeta la paleta), Bloom a ¼ de resolución, Color Adjustments, Vignette. FXAA en la cámara.

Paleta de referencia: azul espacial `#18283A`, azul acero `#30465D`, cian tecnológico `#53C7D8`, blanco suave `#E8EFF4`, naranja marciano `#D88B56`, verde funcional `#65C69A`, rojo de alerta `#DB6262`.

## 9. Cómo cambiar las cinemáticas más adelante (diseño; se detalla en la Fase F)

### Nivel 1 — Modelos y elementos visuales

Coloca los nuevos recursos en `Models/<Elemento>/` (FBX), `Textures/` y `Materials/`, y sustituye según esta tabla:

| Quiero cambiar | Abre / reemplaza | Qué conservar |
|---|---|---|
| Astronauta | `Prefabs/Characters/PF_Astronaut.prefab` → borra el hijo `Visual/Model` y pon tu modelo como hijo de `Visual` llamado **`Model`** | La raíz y `Visual`. Para reutilizar las animaciones, los huesos deben llamarse igual (`Hips/Spine/Chest/Neck/Head`, `UpperArm_L`…); si tu modelo es Humanoid, crea clips Humanoid y sustitúyelos en los Timelines |
| AURA | `PF_AURA.prefab` → `Visual/Model` | Raíz y `Visual`; para sus animaciones, los nombres `Float/Core/Shell/Ring_A/Ring_B/Ring_C/Orbit` |
| Base Aurora (exterior) | `Prefabs/Environment/PF_BaseAurora_Exterior.prefab` | Los objetos `SolarArray` (con sus `Row_*/Panel_*/Tilt`) y `CommsTower/DishPivot` si quieres sus animaciones |
| Terreno / rocas | `PF_Mars_Terrain`, `PF_Mars_RockField` (o el conjunto `PF_Set_MarsSurface`) | — |
| Salas interiores | `PF_Room_ControlCenter`, `PF_Room_PowerRoom`, `PF_Room_Shelter`, `PF_Room_EarthControl` | Grupos `Lights_Normal`, `EmergencyLights`, `BatteryBank/Racks/Rack_NN/Level` y los `Anchor_*` |
| Planetas y Sol | `PF_Space_Mars`, `PF_Space_Earth`, `PF_Space_Sun` | Hijo `Planet` (rotación) y `SolarFlare` |
| Un prop en todas las salas | `Prefabs/Props/PF_Prop_*` (están anidados: se actualizan en todos los escenarios) | — |
| Colores y acabados | `Materials/M_*.mat` (todo usa estos materiales) | El nombre del material |
| Texturas provisionales | Sustituye el PNG de `Textures/Generated` con el mismo nombre, o reasigna la textura en el material | — |
| Cielo | `Materials/Sky/M_Sky_*.mat` o *Window > Rendering > Lighting* de la escena | — |

* Todas las cinemáticas que usan un prefab se actualizan a la vez.
* Para cambiar un modelo solo en una cinemática sin abrir su escena: `CinematicDef_<id>` → **Cast Overrides** → clave del actor (`Astronaut`, `AURA`) + prefab nuevo.
* **No ejecutes** *Fase B · Regenerar recursos (sobrescribe)* después de sustituir recursos: regeneraría los provisionales encima.

### Nivel 2 — Animaciones, cámaras, iluminación y transiciones
* Abre `Scenes/CIN_<id>.unity`, selecciona `[CINEMATIC_ROOT]` y abre *Window > Sequencing > Timeline*: verás `TL_<id>`.
* Duración y orden de planos: mueve/recorta los clips de la pista `Brain`. El solape entre dos clips es la transición (fundido de cámara).
* Encuadre y lente: selecciona la `CM_<id>_NN_*` correspondiente (FOV, composición, ruido). Recorridos: edita la spline `Dolly_*`.
* Animaciones: clips `AC_<id>_*` en `Animations/<id>/` (o graba en la pista con el botón ●).
* Iluminación: objetos de `Lighting/` y pistas de activación/animación de luces. Aspecto: perfil `VP_*` del `GlobalVolume`.
* Fundidos de entrada/salida y omisión: `CinematicDef_<id>`.

### Nivel 3 — Sustituir una cinemática completa (ejemplo: `intro`)
1. Duplica `CIN_Template.unity` (o `CIN_intro.unity`) como `CIN_intro_v2.unity` y crea la nueva secuencia. Mantén un `CinematicSceneRoot` con identificador `intro`.
2. Añade la escena a la build (botón **Añadir a la build** en el selector de escena).
3. En `Config/Definitions/CinematicDef_intro.asset` asigna `CIN_intro_v2` en **Scene**.
4. Listo: el juego sigue llamando a `Play("intro")`. Volver atrás = reasignar la escena anterior.

Variante ligera: crea `TL_intro_v2.playable` respetando los nombres de pista y asígnalo en **Timeline Override**; la escena no se toca.

## 10. Tabla de sustitución (nombres previstos; se confirman al crear cada escena)

| Cinemática | Escena | Timeline | Qué puedo cambiar |
|---|---|---|---|
| Intro | `CIN_intro.unity` | `TL_intro.playable` | Marte, base, astronauta, AURA y cámaras |
| Primera noche | `CIN_sol1_night.unity` | `TL_sol1_night.playable` | Exterior, iluminación, paneles y baterías |
| Alerta solar | `CIN_solar_alert.unity` | `TL_solar_alert.playable` | Actividad solar, centro de control y refugio |
| Tercer sol | `CIN_sol3_start.unity` | `TL_sol3_start.playable` | Amanecer, recorrido y presentación de objetivos |
| Victoria | `CIN_ending_success.unity` | `TL_ending_success.playable` | Informe, transmisión y cierre |
| Derrota | `CIN_ending_failure.unity` | `TL_ending_failure.playable` | Fallos, alertas y luces de emergencia |
| Final parcial | `CIN_ending_partial.unity` | `TL_ending_partial.playable` | Estado de la base, experimentos y cierre |

## 11. Presupuesto para Android de gama media

| Recurso | Objetivo |
|---|---|
| Triángulos visibles por plano | ≤ 150 000 |
| Astronauta / AURA / módulo de base | ≤ 15 000 / ≤ 5 000 / ≤ 40 000 tris |
| Texturas | 1024² personajes y props, 2048² máx. en escenario principal; ASTC; mipmaps |
| Luces | 1 direccional con sombra (1 cascada) + ≤ 4 adicionales por objeto, sin sombras adicionales |
| Partículas | ≤ 3 sistemas en pantalla, ≤ 500 partículas, materiales unlit |
| Post-procesado | Tonemapping, Bloom ¼, Color Adjustments, Vignette, FXAA (sin SSAO ni DOF costoso) |
| Audio | Música en *Streaming* (Vorbis), voces y SFX comprimidos |
| Rendimiento | 30 fps estables en cinemáticas; escena descargada y recursos liberados al terminar |

## 12. Plan por fases

| Fase | Contenido | Archivos principales que se crearán |
|---|---|---|
| **A** ✔ | Inspección, paquetes, arquitectura, carpetas, render, Timeline/Cinemachine, plantilla | Scripts de §3, catálogo, 7 definiciones, `CIN_Template`, `TL_Template` |
| **B** ✔ | Personajes, materiales de paleta, prefabs, animaciones base, escenarios, espacio, efectos, galería | Ver `FaseB_Informe.md` (pendiente de tu prueba en Unity) |
| **C** ✔ | `intro`, `sol1_night`, `solar_alert`, `sol3_start` | Ver `FaseC_Informe.md` (pendiente de tu prueba en Unity) |
| **D** ✔ | `ending_success`, `ending_failure`, `ending_partial` | Ver `FaseD_Informe.md` (pendiente de tu prueba en Unity) |
| **E** ✔ | `CinematicManager`, carga/descarga, fundidos, subtítulos, audio, omisión, retorno al 2D, puente, banco de pruebas | Ver `FaseE_Informe.md` (pendiente de tu prueba en Unity) |
| **F** ✔ | Pruebas automáticas (EditMode + PlayMode), optimización y medición Android, documentación final | Ver `Entrega_Final.md` y `FaseF_Pruebas.md` |

## 13. Cómo probar la Fase A

1. Vuelve a la ventana de Unity y espera a que termine de resolver paquetes (Cinemachine/Splines) y de compilar. La Consola no debe mostrar errores en rojo.
2. Menú **SOL-3 > Cinemáticas > Fase A · Configurar proyecto** → *Configurar*.
3. Resultado esperado: 0 errores. Aparecen **PENDIENTES** (escena y subtítulos de cada cinemática: se crean en C/D) y probablemente 2 **AVISOS** (Package Name de Android y orientación vertical permitida). Si aparece un aviso de TextMesh Pro es porque su importación aún no había terminado: ejecuta **SOL-3 > Cinemáticas > Validar sistema** de nuevo.
4. Se abre `CIN_Template`. Pulsa **Play**: 10 s con travelling en dolly hacia el hábitat, fundido de cámara (4,5–6 s) a un plano cerrado con cámara en mano del cubo cian flotando, y luz roja de alerta a los 6,5 s. La Consola muestra el inicio y "terminada (Completed)"; el último fotograma se mantiene.
5. Con `[CINEMATIC_ROOT]` seleccionado, abre *Window > Sequencing > Timeline*: pistas `CÁMARAS (Brain, CM_Template_Wide)`, `ACTORES (TestActor)`, `ILUMINACIÓN (AlertLight)`, `AUDIO (Music)`.
6. Comprueba que el 2D no cambió: abre `Assets/Scenes/SampleScene` y pulsa Play; en `Assets/Settings/UniversalRP` el renderer por defecto sigue siendo `Renderer2D`.
7. Revisa `Config/CinematicCatalog.asset` y cualquier `CinematicDef_<id>`.
8. (Opcional) Android: define el *Package Name* y usa **SOL-3 > Cinemáticas > Android · Build de prueba de la plantilla**.

Informes: Consola y `Logs/SOL3_Cinematics_Setup.txt`, `Logs/SOL3_Cinematics_Validation.txt`.

## 14. Cómo probar la Fase B

1. En Unity: **SOL-3 > Cinemáticas > Fase B · Crear recursos y galería** → *Crear* (si la Fase A no estaba configurada se configura antes; puede pedirte guardar la escena abierta). Tarda 1-2 minutos.
2. Resultado esperado: 0 errores. Se abre `CIN_FaseB_Galeria`.
3. Pulsa **Play** (~84 s): astronauta (8 animaciones) → AURA (aparición, reposo, habla, alerta) → exterior de Base Aurora en dolly con paneles solares orientándose y polvo → sala de control (se activa la emergencia) → sala de máquinas (baterías bajando y chispas) → refugio → Control Tierra → Marte → el Sol con erupciones.
4. Revisa en el Project los prefabs de `Prefabs/Characters`, `Prefabs/Environment`, `Prefabs/Props`, `Prefabs/FX` (doble clic abre el modo prefab).
5. Con `[CINEMATIC_ROOT]` seleccionado, *Window > Sequencing > Timeline* muestra `TL_FaseB_Galeria`: puedes arrastrar el cursor para previsualizar.

Informe: Consola y `Logs/SOL3_Cinematics_PhaseB.txt`.

## 15. Cómo probar la Fase C

1. **SOL-3 > Cinemáticas > Fase C · Generar cinemáticas narrativas** → *Generar* (≈1 min). Resultado esperado: 0 errores; se abre `CIN_intro`.
2. Pulsa **Play**: la cinemática se reproduce con bandas negras y subtítulos. Repite con `CIN_sol1_night`, `CIN_solar_alert` y `CIN_sol3_start` (en `Assets/Cinematics3D/Scenes`).
3. Pon la vista Game en horizontal (16:9 o 2:1) para ver la composición prevista.
4. *SOL-3 > Cinemáticas > Validar sistema*: las cuatro cinemáticas aparecen con escena en la build y subtítulos (las tres de final siguen pendientes de la Fase D).

Informe: `Logs/SOL3_Cinematics_PhaseC.txt`.

## 16. Cómo probar la Fase D

1. **SOL-3 > Cinemáticas > Fase D · Generar finales** → *Generar* (≈1 min). Resultado esperado: 0 errores; se abre `CIN_ending_success`.
2. Vista Game en horizontal y **Play**. Repite con `CIN_ending_failure` y `CIN_ending_partial`.
3. *SOL-3 > Cinemáticas > Validar sistema*: si ya generaste también la Fase C, las siete cinemáticas aparecen con escena en la build y subtítulos (0 pendientes de contenido).

Informe: `Logs/SOL3_Cinematics_PhaseD.txt`.

## 17. Cómo probar la Fase E

1. Si no lo has hecho: **Fase C · Generar cinemáticas narrativas** y **Fase D · Generar finales**.
2. **SOL-3 > Cinemáticas > Fase E · Crear banco de pruebas** → se abre `SOL3_CinematicTestHarness`.
3. Vista Game en horizontal y **Play**. Pulsa los botones: cada cinemática debe fundir a negro, reproducirse con subtítulos y botón *Omitir*, y volver al banco con el estado simulado (energía, oxígeno, tiempo) igual que estaba.
4. Pruebas de error: *id desconocido* (rechazada), *escena ausente* (termina como `Failed` y vuelve), *petición duplicada* (la segunda se rechaza). Las cinemáticas "una vez por partida" no se repiten hasta pulsar *Partida nueva*.

Informe: `Logs/SOL3_Cinematics_PhaseE.txt` y la Consola (`[Cinematics]`).
