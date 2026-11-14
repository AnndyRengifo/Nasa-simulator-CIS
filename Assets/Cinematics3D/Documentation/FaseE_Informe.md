# Fase E — Sistema de integración · Informe (14/11/2026)

## Componentes nuevos (runtime, `SOL3.Cinematics`)

| Script | Función |
|---|---|
| `CinematicManager` | Implementa `ICinematicService`: reproduce por identificador, rechaza duplicados, carga aditiva con límite de tiempo, fundidos, subtítulos, audio, omisión, límite de seguridad, descarga y liberación de recursos, vuelta al juego 2D o carga de la escena de retorno, y aviso de fin exactamente una vez. Nunca decide ni cambia el resultado de la partida. |
| `CinematicOverlay` | Interfaz por código: fundido a color, botón **Omitir** táctil y presentador de subtítulos. Crea un EventSystem (Input System) solo si no hay ninguno. Usa tiempo no escalado. |
| `CinematicAudioSettings` | Volúmenes Master / Música / Voz / SFX (0-1, guardados en PlayerPrefs) para el menú de opciones del juego. Se aplican a un AudioMixer si el catálogo lo tiene; si no, directamente a las fuentes. |
| `SimpleCinematicGameBridge` | Puente listo para usar: eventos *On Starting* / *On Finished* para pausar y reanudar el juego 2D desde el Inspector, y registro de cinemáticas vistas por ranura de partida. |
| `CinematicRequester` | Pedir cinemáticas desde UnityEvents/botones sin código. |
| `CinematicTestHarness` | Banco de pruebas que simula el juego 2D (solo para pruebas). |

Ajustes globales nuevos en `CinematicCatalog`: desactivar cámaras/AudioListeners del juego durante la cinemática, pausar el audio del juego, AudioMixer opcional con nombres de parámetros, texto del botón Omitir.

## Flujo de reproducción

1. Fundido a negro → `OnCinematicStarting` (el juego pausa y guarda su estado) → `Time.timeScale = 0` y pausa del audio del juego (configurable).
2. Carga aditiva de `CIN_<id>` (comprueba que esté asignada y en la build; límite de tiempo).
3. Preparación del `CinematicSceneRoot`: Timeline de sustitución, modelos de sustitución y enlaces por convención; la escena 3D pasa a ser la activa (su cielo y su iluminación).
4. Se desactivan las cámaras y AudioListeners del juego; subtítulos en el idioma activo; volúmenes.
5. Reproducción. **Omitir**: botón en pantalla y botón Atrás de Android, solo si la definición lo permite y tras *Skip Available After*. Límite de seguridad: duración prevista + margen.
6. Fundido a negro, descarga de la escena, `Resources.UnloadUnusedAssets`.
7. Se restauran cámaras, `timeScale` y audio → `MarkCinematicShown` (si se vio) → `OnCinematicFinished`. Si la definición dice *LoadScene*, se carga su escena de retorno.
8. Fundido de vuelta y `onFinished` / `CinematicFinished`.

**Errores** (escena ausente, fuera de la build, sin `CinematicSceneRoot`, sin Timeline, carga lenta): termina con `Failed`, se avisa en la Consola y **el juego continúa** con su estado intacto.

## Cómo lo integra el juego 2D

1. En la escena inicial del juego: un objeto con `CinematicManager` (catálogo `Config/CinematicCatalog`) y un puente (`SimpleCinematicGameBridge` o una clase propia que implemente `ICinematicGameBridge`). El gestor persiste entre escenas.
2. La cámara 2D debe **excluir la capa `Cinematic3D`** de su *Culling Mask*.
3. La lógica del juego pide cada cinemática cuando corresponde:

```csharp
using Sol3.Cinematics;

// Nueva partida
CinematicManager.Instance.Play(CinematicIds.Intro);

// Al comenzar la primera noche / el evento solar / el tercer sol
CinematicManager.Instance.Play(CinematicIds.Sol1Night);
CinematicManager.Instance.Play(CinematicIds.SolarAlert);
CinematicManager.Instance.Play(CinematicIds.Sol3Start);

// Final: la lógica decide el resultado ANTES y después pide la cinemática
string ending = outcome == Outcome.Victory ? CinematicIds.EndingSuccess
              : outcome == Outcome.Defeat  ? CinematicIds.EndingFailure
              : CinematicIds.EndingPartial;
CinematicManager.Instance.Play(ending, result => ShowResultsScreen());
```

4. En `OnCinematicStarting` / `OnCinematicFinished` del puente, el juego pausa y reanuda sus mecánicas (temporizadores, controles). Si usa `Time.timeScale`, el gestor ya lo gestiona.

## Banco de pruebas

`Assets/Cinematics3D/Tests/SOL3_CinematicTestHarness.unity` (menú **Fase E · Crear banco de pruebas**): cámara 2D, gestor real, puente y botones para las 7 cinemáticas y las pruebas de error. Muestra un estado de partida simulado (sol, energía, oxígeno, tiempo) que se congela durante la cinemática y continúa con los mismos valores.

Menú **Android · Build de prueba del sistema completo**: APK con el banco de pruebas como primera escena y todas las cinemáticas.

## Limitaciones conocidas

* No se crea un AudioMixer automáticamente (Unity no ofrece API pública para ello). Para usarlo: *Assets > Create > Audio Mixer*, expón los parámetros `CinematicMaster/Music/Voice/SFX` y asígnalo en el catálogo. Sin él, los volúmenes se aplican directamente.
* Las cinemáticas aún no tienen música ni voces grabadas: las pistas de audio y los campos de voz de los subtítulos están preparados para recibirlas.

## Verificación hecha

* Compilación offline con el Roslyn de Unity 6.6 (Editor y perfil Android IL2CPP): **sin errores ni avisos**.

## Pendiente de verificar (requiere el Editor / dispositivo)

* Recorrer el banco de pruebas en Play; APK del sistema en un dispositivo Android (Fase F).
