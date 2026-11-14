# Fase C — Cinemáticas narrativas · Informe (14/11/2026)

Menú: **SOL-3 > Cinemáticas > Fase C · Generar cinemáticas narrativas**. Regenera las cuatro escenas cada vez que se ejecuta (no toca los recursos de la Fase B). Cada escena queda registrada en su `CinematicDef_<id>` (escena, subtítulos, duración) y en la lista de escenas de la build.

Personajes y escenarios siguen siendo los **provisionales** de la Fase B; al sustituir sus prefabs, las cinemáticas se actualizan solas.

## Nuevos componentes de runtime

| Script | Uso |
|---|---|
| `CinematicSubtitlePresenter` | Bandas negras adaptadas al dispositivo (composición 2:1) y subtítulos TextMeshPro sincronizados con el `PlayableDirector`; nombre del personaje con su color; reproduce la voz si la línea la tiene. Lo usa la vista previa y lo reutilizará el `CinematicManager` (Fase E). |
| `CinematicEnvironmentProfile` | Cielo, niebla y luz ambiente de un tramo (espacio, día, atardecer, noche). Se activa con pistas de activación del Timeline; funciona también en la vista previa del Editor. |
| `CinematicSceneRoot` | Nuevos campos de vista previa: `Preview Subtitles` y `Preview Aspect`. |

## Las cuatro cinemáticas

### `intro` — `CIN_intro` / `TL_intro` (≈76 s, 8 planos)

| Tiempo | Plano | Qué ocurre |
|---|---|---|
| 0–11 s | `CM_intro_01_MarsOrbit` (dolly) | Marte desde el espacio, acercamiento. Control Tierra: "Durante generaciones…", "…hasta que finalmente aprendimos a vivir aquí." |
| 11–22 s | `02_Descent` (dolly) | Descenso hacia la superficie y la base. "Esta es Aurora…" |
| 22–34 s | `03_BaseOrbit` (dolly) | Recorrido exterior por la colonia; los paneles solares se orientan |
| 34–41 s | `04_Interior` (dolly) | Centro de control |
| 41–48 s | `05_Astronaut` | Presentación del astronauta (consulta la muñeca, mira alrededor) |
| 48–68 s | `06_AuraReveal`, `07_AuraClose` | AURA aparece en la mesa holográfica: "Bienvenido…", "Soy AURA…", "Durante los próximos tres soles…", "Todos los sistemas están operativos." |
| 68–76 s | `08_Closing` (dolly) | "Por ahora." con un parpadeo de las luces; fin y transición al juego |

### `sol1_night` — `CIN_sol1_night` (≈33 s, 5 planos)

Atardecer con el sol bajando y perdiendo intensidad → los paneles solares se pliegan y su indicador se apaga → sala de máquinas, las baterías empiezan a descargarse → AURA avisa ("Atención. La producción solar…", "Durante la noche…", "Espero que hayas administrado bien…") → la base de noche.

### `solar_alert` — `CIN_solar_alert` (≈41 s, 6 planos)

El Sol desde el espacio con erupciones (sin cifras ni mediciones inventadas) → ráfaga de actividad → alerta en el centro de control (luces de emergencia, pantalla roja, el astronauta revisa y se preocupa) → AURA analiza en modo alerta → el refugio con otro astronauta → plano final para las decisiones del jugador. Cuatro líneas de AURA.

### `sol3_start` — `CIN_sol3_start` (≈40 s, 6 planos)

Amanecer marciano (cielo azulado, el sol sube) → recorrido por la base → paneles orientándose y baterías cargándose → el astronauta se prepara en la consola → AURA presenta los objetivos finales. Cuatro líneas de AURA.

## Dónde cambiar cada cosa

* **Textos y tiempos de diálogo**: `Subtitles/SUB_<id>_es.asset` (no hace falta tocar el Timeline).
* **Planos, duración, orden y transiciones**: `TL_<id>` en la ventana Timeline (pista `Brain`); encuadres en los objetos `CM_<id>_*` y recorridos en `Dolly_<id>_*` de la escena.
* **Acciones**: clips de las pistas `Astronaut`, `AURA`, `SolarArray`, `BatteryBank`; animaciones de cámara y sol en `Animations/<id>/`.
* **Luz y ambiente**: pista `SunRig` (sol) y pistas `Env_*` (cielo/niebla); objetos de `Lighting` en la escena.

## Verificación hecha

* Compilación offline con el Roslyn de Unity 6.6 (Editor y perfil Android IL2CPP): **sin errores ni avisos**.

## Pendiente de verificar (requiere el Editor)

* Generar las escenas en Unity y revisarlas en Play. Encuadres y tiempos pueden necesitar ajustes finos tras verlas.
