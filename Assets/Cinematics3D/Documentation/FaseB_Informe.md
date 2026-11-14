# Fase B — Recursos y personajes · Informe (14/11/2026)

## Aviso honesto sobre la calidad

No hay modelos 3D en el proyecto, así que **todo es provisional**: personajes y escenarios construidos con primitivas de Unity y mallas procedurales generadas por código (terreno, cúpulas, anillos, icoesferas, conos), con materiales de la paleta del proyecto y texturas procedurales. Tienen proporciones, colores e iluminación coherentes con la dirección artística, pero **no son modelos de calidad profesional**. Están estructurados para sustituirse sin tocar cinemáticas (ver README §9).

## Cómo se genera

Menú **SOL-3 > Cinemáticas > Fase B · Crear recursos y galería**. Es idempotente: solo crea lo que falta (si la Fase A no estaba configurada, la ejecuta antes). **Regenerar recursos (sobrescribe)** rehace los provisionales conservando los GUID, así que las escenas que los usan no pierden referencias.

## Recursos creados

### Personajes (`Prefabs/Characters`)

| Prefab | Contenido | Animaciones (`Animations/…`) |
|---|---|---|
| `PF_Astronaut` | Raíz con Animator (controlador en bucle *Idle*) + `CinematicBindingTarget` clave `Astronaut` → `Visual` → `Model` (`Models/PF_Astronaut_Model`): traje blanco y grafito, franjas naranjas, visor oscuro (rostro no visible), mochila, panel de pecho y de muñeca con luces. Esqueleto: `Hips/Spine/Chest/Neck/Head`, brazos y piernas `_L/_R`. ~17 000 triángulos (algo por encima del objetivo de 15 000 porque las primitivas esféricas de Unity son densas; un modelo final lo resolverá). | `AC_Astronaut_Idle` (bucle), `Walk` (bucle, baja gravedad), `LookAround`, `Gesture`, `WorkConsole` (bucle), `CheckWrist`, `Celebrate`, `Concern` |
| `PF_AURA` | Raíz con Animator (bucle *Idle*) + clave `AURA` → `Visual` → `Model` (`PF_AURA_Model`): núcleo icosaédrico facetado emisivo cian, envoltura holográfica, tres anillos en precesión y nodos orbitales. Geométrica y sobria (no es una mascota). | `AC_AURA_Idle`, `Speak` (pulsos de habla), `Alert` (tono rojo), `Appear` |

### Escenarios (`Prefabs/Environment`)

| Prefab | Contenido |
|---|---|
| `PF_Set_MarsSurface` | Conjunto: `PF_Mars_Terrain` (400 × 400 m, relieve y cráteres, explanada central) + `PF_Mars_RockField` (48 rocas) + `PF_BaseAurora_Exterior` |
| `PF_BaseAurora_Exterior` | Cúpula central con banda de ventanas, 3 módulos (laboratorio, hábitat, sistemas), invernadero de cristal con plantas, 12 paneles solares (`SolarArray`, animables), estación de energía, torre de comunicaciones con antena en barrido, plataforma de aterrizaje, postes de luz |
| `PF_Room_ControlCenter` | Centro de control: ventana a Marte, 3 consolas + consola de comunicaciones, mesa holográfica (anclaje de AURA), luces normales y grupo `EmergencyLights` (apagado) |
| `PF_Room_PowerRoom` | Sala de máquinas: 8 bastidores de baterías (`BatteryBank`, nivel animable), convertidor con bandas naranjas, tuberías, consola, emergencia |
| `PF_Room_Shelter` | Refugio: blindaje, literas, depósitos de agua, cajas, puerta con franjas de peligro, luz verde de seguridad |
| `PF_Room_EarthControl` | Control Tierra: pantalla gigante de misión (Tierra → Marte), 8 puestos con operadores (siluetas), iluminación fría |
| `PF_Space_Mars`, `PF_Space_Earth`, `PF_Space_Sun` | Planetas texturizados con atmósfera y rotación; Sol emisivo con corona y erupciones (`SolarFlare`) |

### Props, efectos y materiales

* **Props** (`Prefabs/Props`, anidados en los escenarios): `PF_Prop_Console`, `PF_Prop_HoloTable`, `PF_Prop_BatteryRack`, `PF_Prop_AlertBeacon` (giratoria), `PF_Prop_Crate`, `PF_Prop_SolarPanel`.
* **Efectos** (`Prefabs/FX`): `PF_FX_MarsDust`, `PF_FX_SolarFlare`, `PF_FX_Sparks` (gravedad marciana 0,38 g), `PF_FX_TransmissionPulse`. Todos con tiempo no escalado y pocas partículas.
* **Animaciones de props** (`Animations/Props`): `AC_SolarArray_Track`, `AC_SolarArray_Stow`, `AC_Battery_Charge`, `AC_Battery_Drain`, `AC_Beacon_Spin`, `AC_Dish_Scan`, `AC_Planet_Rotate`.
* **Materiales** (`Materials/M_*`): traje, base, cristal, paneles solares, Marte, Tierra, Sol, emisivos de la paleta, pantallas, hologramas y partículas. **Cielos** (`Materials/Sky`): `M_Sky_MarsDay`, `M_Sky_MarsDusk` (atardecer azulado, como en Marte), `M_Sky_MarsNight`, `M_Sky_Space` (estrellas).
* **Texturas** (`Textures/Generated`, PNG): estrellas, Marte, Tierra, Sol, suelo, paneles, células solares, franjas de peligro, pantallas (datos, alerta, misión), vista por la ventana, partícula suave.

## Escena de prueba

`Scenes/CIN_FaseB_Galeria.unity` + `Timelines/TL_FaseB_Galeria.playable` (~84 s, 9 planos con cortes): astronauta con sus 8 animaciones, AURA con las 4, recorrido en dolly del exterior con paneles orientándose, sala de control con emergencia, baterías descargándose con chispas, refugio, Control Tierra, Marte y el Sol. No va en la build del juego.

## Verificación hecha

* Compilación offline con el Roslyn de Unity 6.6 contra las DLL reales: **sin errores ni avisos**, también sin Cinemachine y en perfil Android IL2CPP.

## Pendiente de verificar (requiere el Editor)

* Ejecutar el menú de la Fase B, revisar la galería en Play y el rendimiento en un dispositivo Android.
