# Fase A — Preparación · Informe (14/11/2026)

## Inspección del proyecto

* **Unity 6000.6.9f1** (Unity 6.6), creado con la plantilla *2D cross-platform*. No se cambia la versión.
* **Render:** URP 17.6.0 con un único renderer `Renderer2D`. Con él, la geometría 3D no recibe iluminación 3D real → se necesita un renderer 3D adicional.
* **Paquetes:** Timeline 6.6.0 (builtin) instalado; Cinemachine **no** instalado (6.6.0 viene incluido en el Editor); TextMesh Pro incluido en uGUI 2.6.0; Input System 1.20.1 como único sistema de entrada.
* **Calidad:** 6 niveles, todos con `UniversalRP.asset`; Android usa *Medium*. HDR activado, MSAA desactivado, sombra principal 2048 px (1 cascada, 50 m), 4 luces adicionales por píxel sin sombras, SRP Batcher activo.
* **Android:** Build Support instalado (SDK, NDK, OpenJDK). IL2CPP, ARM64, API mínima 26, objetivo automático, espacio de color Linear, APIs gráficas automáticas.
* **Juego 2D:** el proyecto todavía **no contiene** el juego 2D (solo `Assets/Scenes/SampleScene.unity`). La integración se diseña con una interfaz (`ICinematicService` / `ICinematicGameBridge`) para no depender de su implementación.

## Cambios realizados

| Cambio | Motivo | Reversible |
|---|---|---|
| `Packages/manifest.json`: `com.unity.cinemachine` 6.6.0 | Cámaras cinemáticas; versión builtin del propio Editor (compatible). Arrastra Splines 2.9.2. | Sí (quitar la línea) |
| `Assets/Cinematics3D/` con scripts (runtime + editor) y documentación | Arquitectura de la Fase A | Sí (borrar carpeta) |
| `Packages/manifest.json`: retirado `com.unity.ai.assistant` 2.9.0-pre.2 | Paquete preliminar incluido por la plantilla que no compila en Unity 6000.6.9f1 (`error UAC0020` en `DynamicAssemblyBuilder.cs`, por usar `Assembly.Load(byte[])`). Dejaba el proyecto en **Safe Mode** desde su creación. No lo usan el juego ni las cinemáticas; `collab-proxy` solo lo usa tras `#if AIA_PRESENT`. | Sí: se puede reinstalar desde el Package Manager cuando haya una versión compatible |

Cambios que hará el asistente **cuando lo ejecutes en Unity** (*SOL-3 > Cinemáticas > Fase A · Configurar proyecto*):

| Cambio | Motivo |
|---|---|
| Capa `Cinematic3D` en *Tags and Layers* | Aislar 3D y 2D aunque ambas escenas estén cargadas |
| `CinematicUniversalRenderer` añadido al final de la lista de `UniversalRP.asset` | Iluminación 3D real solo en cámaras cinemáticas; el 2D sigue por defecto |
| Catálogo, 7 definiciones, personajes, perfiles de ruido, post-procesado, ajustes de iluminación | Configuración central y estándar visual |
| Recursos esenciales de TextMesh Pro (`Assets/TextMesh Pro`) | Subtítulos |
| `CIN_Template.unity` + `TL_Template.playable` + animaciones y materiales `PH_` | Plantilla y prueba de humo de Timeline + Cinemachine + renderer 3D |

## Verificación hecha

* Compilación de los ensamblados `SOL3.Cinematics` y `SOL3.Cinematics.Editor` con el compilador Roslyn incluido en Unity 6.6, contra las DLL reales del proyecto y con Splines y Cinemachine compilados desde sus fuentes builtin: **sin errores ni avisos**.
* Compilación de `SOL3.Cinematics` con el perfil de jugador **Android IL2CPP** (.NET Standard 2.1 + módulos del reproductor Android): **sin errores** (el código runtime no usa APIs exclusivas del Editor).
* Compilación sin Cinemachine presente (primera compilación antes de resolver el paquete): **sin errores**.
* En el propio Unity (Editor.log, 14:33): Cinemachine 6.6.0 y Splines 2.9.2 registrados; `Unity.Cinemachine`, `SOL3.Cinematics` y `SOL3.Cinematics.Editor` **compilados sin errores**. El único error del proyecto era el del paquete `com.unity.ai.assistant` (ya presente en la primera compilación al crear el proyecto, antes de estos cambios).

## Pendiente de verificar (requiere el Editor)

* Ejecución del asistente y creación de assets/escena dentro de Unity.
* Reproducción de `CIN_Template` en modo Play.
* Build de prueba en un dispositivo Android.

## Decisiones abiertas

* **Orientación:** el proyecto permite vertical. Las cinemáticas se componen en horizontal (aspecto 2:1 con bandas automáticas). Recomendado: fijar Landscape.
* **Package Name de Android:** no hay uno válido definido (el de Standalone, `com.DefaultCompany.2D-URP`, no es válido en Android).
* **Juego 2D:** dónde vivirá y quién implementa `ICinematicGameBridge` (afecta a la Fase E).
