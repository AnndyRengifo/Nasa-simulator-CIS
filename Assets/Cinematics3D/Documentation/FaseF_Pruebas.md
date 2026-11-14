# Fase F — Pruebas y optimización (14/11/2026)

## Pruebas automáticas

Menú **SOL-3 > Cinemáticas > Fase F · Ejecutar pruebas automáticas** (o *Window > General > Test Runner*). Resultados en `Logs/SOL3_Cinematics_Tests.txt`.

**EditMode — `CinematicContentTests`** (por cada una de las 7 cinemáticas):
* El catálogo contiene los 7 identificadores, sin duplicados.
* Escena asignada, existente y en la build; omisión permitida; subtítulos en español válidos (sin solapes, con personaje, dentro de la duración).
* Contrato de escena: `CinematicSceneRoot` con el id correcto, director sin *Play On Awake*, Timeline con todas las pistas enlazadas, planos Cinemachine con cámara, cámara con Brain, AudioListener y *Culling Mask* = `Cinematic3D`, todos los objetos en la capa `Cinematic3D`, duración prevista coherente.
* Presupuesto Android: triángulos, luces, sombras adicionales (0), partículas, texturas, materiales; sin GI horneada.
* **Sustitución de Nivel 3**: se borran todos los enlaces del director y se comprueba que el enlazador por convención los restaura.
* Personajes dentro de su presupuesto de triángulos (ejecuta antes *Optimizar y medir*).

**PlayMode — `CinematicManagerTests`** (juego 2D simulado):
* Cada cinemática: se acepta; una segunda petición simultánea se rechaza; pausa el juego (`timeScale` 0, cámara y AudioListener 2D desactivados); la escena 3D se carga; se puede omitir; termina como `Skipped`; restaura `timeScale`, cámara, AudioListener y audio; descarga la escena; avisa al puente y la registra como vista.
* Identificador desconocido → rechazado sin tocar el juego.
* Escena ausente → `Failed` y el juego recupera el control.
* "Una vez por partida" → no se repite.
* `Stop()` → `Stopped` y el juego recupera el control.

### Resultado de la ejecución (14/11/2026, 16:00)

| Conjunto | Superadas | Fallidas | Omitidas |
|---|---|---|---|
| EditMode (contenido) | **30** | 0 | 0 |
| PlayMode (integración) | **11** | 0 | 0 |
| **Total** | **41** | **0** | **0** |

Cada cinemática tardó ≈4,1 s en la prueba de integración (fundido, carga, omisión, descarga y restauración). Detalle en `Logs/SOL3_Cinematics_Tests.txt`.

Optimización aplicada: astronauta **16 776 → 11 848 triángulos**; AURA **4 804 triángulos** (ambos dentro del presupuesto). La tabla de rendimiento por escena se genera con *Fase F · Optimizar y medir rendimiento* en `Logs/SOL3_Cinematics_Performance.md`; las pruebas EditMode ya comprueban que las 7 escenas están dentro del presupuesto.

## Pruebas manuales ya realizadas (registro del Editor, 14/11/2026)

| Prueba | Resultado |
|---|---|
| Fases B, C, D y E generadas en Unity | 0 errores |
| Validación del sistema | 28 OK · 0 pendientes · 0 errores · 1 aviso (orientación vertical permitida) |
| Banco de pruebas: `intro`, `sol1_night`, `ending_success`, `ending_failure` | Inician, se omiten y vuelven al juego (`Skipped`) |
| Identificador desconocido | Rechazado |
| Escena ausente | `Failed`, la partida continúa |
| Repetir `sol1_night` en la misma partida | Rechazada ("ya se mostró") |
| Galería de la Fase B en Play | Se reproduce |
| Consola durante esas pruebas | Sin excepciones ni avisos |

## Optimización para Android

* **Astronauta**: esferas de 768 → 320 triángulos (≈16 600 → ≈11 600 triángulos), mismo aspecto y animaciones (menú *Fase F · Optimizar y medir rendimiento*).
* Iluminación 100 % en tiempo real, sin lightmaps; 1 luz direccional con sombra; luces adicionales sin sombra y limitadas por capa.
* Post-procesado ligero (Bloom a ¼, Tonemapping Neutral, Vignette, Color Adjustments, FXAA).
* Partículas con pocas unidades y tiempo no escalado; materiales con SRP Batcher/instancing.
* Escenas cargadas de forma aditiva solo durante su reproducción; descarga y `Resources.UnloadUnusedAssets` al terminar.
* Medición por escena (triángulos, materiales, texturas estimadas en ASTC, luces, partículas): `Logs/SOL3_Cinematics_Performance.md`.

## Pendiente (no verificable desde aquí)

* **Dispositivo Android real**: generar *Android · Build de prueba del sistema completo* y comprobar fluidez (objetivo 30 fps), subtítulos legibles, botón Omitir y botón Atrás en un móvil de gama media.
* Decisión del juego 2D: fijar orientación **Landscape** (las cinemáticas se componen en horizontal).
* Música y voces reales (el sistema ya las admite).
