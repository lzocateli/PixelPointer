# Plan de implementación — PixelPointer

[Português (Brasil)](../plano-implementacao.md) · [English](../en/plano-implementacao.md) · **Español**

| Campo | Valor |
| --- | --- |
| Estado inicial | Aprobado para F0 (decisiones del 2026-10-29) |
| Estado del plan (2026-11-04) | **Activo**. La sección 8 es la única fuente de pendientes de implementación, validación, decisión y documentación |
| Estado de entrega (2026-11-04) | v0.3.1 es la última versión estable publicada. F0/F1 están entregadas; zoom, anotación, temporizador, recorte, Live Zoom y GIF están implementados. La aceptación GNOME/X11 del 2026-11-04 cubre manualmente las funciones disponibles en un paquete local, pero no es una release publicada ni una validación multiplataforma. Consulte los [pendientes consolidados](#8-pendientes-consolidados) |
| Primera versión funcional | v0.1.2, aceptada como estable por el mantenedor el 2026-10-30 en Ubuntu 24.04.5/GNOME X11 y Ubuntu 26.04/GNOME Wayland; aceptación manual limitada a flujos/entornos registrados, sin completar F2/F3 ni validar Windows/macOS |
| Origen | Asistido por IA |
| Revisor humano | Lincoln Zocateli |
| Repositorio | <https://github.com/lzocateli/PixelPointer> |
| Licencia | MIT |

**Aceptación complementaria del 2026-10-30:** el solicitante confirmó dibujo
transparente, zoom, exportación PNG con alfa, CapsLock/NumLock y uso en ambos
monitores 1920×1080 con escala 1 en Ubuntu 24.04.5/GNOME X11, autorizando dar
esas pruebas por concluidas. El [registro de evidencias y límites](plano-e2e-grafico-uv.md#aceptaci%C3%B3n-manual-del-dibujo-transparente-en-gnome-x11)
separa la aceptación manual del E2E automatizado. En la fecha de esa aceptación,
F3 seguía incompleta; las formas, el texto y el blur se implementaron después.
Las brechas actuales de F3 son multimonitor Wayland y validación por
plataforma/topología, como indica la sección 8. Capturas complementarias de la misma fecha
muestran anotaciones en el monitor 1 y el icono de bandeja GNOME visible con el
menú Zoom/Desenhar/Sair abierto; ese alcance visual está verificado. El solicitante
también confirmó el cierre mediante Sair y aportó una captura con regreso al prompt
y `status` indicando ausencia del daemon. Ese flujo tiene aceptación manual en
la misma sesión GNOME/X11, sin demostrar inicio sin terminal.

**Aceptación manual Ubuntu 26.04/GNOME Wayland del 2026-10-30:** tras instalar
la release `v0.1.2`, el solicitante informó que todo funcionaba y adjuntó seis
capturas a esta conversación. La aceptación cubre los flujos ejercitados en esa
sesión según el relato humano. Las capturas muestran anotaciones sobre la imagen
del escritorio, pizarras blanca y negra conservando los mismos trazos, regreso
al fondo capturado, icono propio en el lanzador y menú Zoom/Desenhar/Sair en la
bandeja. El [registro detallado](plano-e2e-grafico-uv.md#aceptaci%C3%B3n-manual-en-ubuntu-2604gnome-wayland)
separa observación visual y relato: imágenes estáticas no demuestran fondo en vivo,
alfa del PNG, activación desde lanzador/bandeja, latencia ni backend de captura.
No completa F2/F3 ni extiende la aceptación a otros compositores, monitores,
escalas, Windows o macOS. El E2E Wayland automatizado sigue pendiente. Los adjuntos
permanecen en la conversación, sin copia al repositorio.

**Evidencia complementaria de transparencia en la misma fecha:** el solicitante
adjuntó otras dos capturas, identificándolas como evidencia de dibujo transparente.
Ambas muestran trazos rojos sobre un vídeo visible en el navegador, con contenidos
diferentes. El dibujo transparente tiene aceptación manual en esta sesión Ubuntu
26.04/GNOME Wayland, con relato humano y evidencia visual de la superposición.
Las capturas no demuestran reproducción continua bajo el mismo trazo ni alfa PNG;
esas comprobaciones objetivas siguen separadas de la aceptación manual.

**Aceptación de preview el 2026-10-31:** el solicitante probó
`0.1.3-preview.14` en Ubuntu 26.04.1 LTS x64/GNOME Wayland, adjuntó una
captura del consentimiento ScreenCast y un log de captura mediante PipeWire,
y confirmó explícitamente zoom y dibujo como funcionales. Captura, zoom y
dibujo tienen aceptación manual en esa máquina. El PNG fue de 1920×1080;
el portal tardó 20.830 ms incluyendo interacción con el diálogo, el primer
cuadro 122 ms y el total 20.999 ms. No demuestra la meta de atajo a primer
cuadro < 150 ms, reutilización del token, duración del indicador, atajos
físicos, bandeja, lanzador, Ctrl+S ni transparencia en vivo. Otros monitores,
escritorios y Windows/macOS no heredan aceptación.
[Evidencias y límites](plano-e2e-grafico-uv.md#aceptación-manual-de-preview-en-gnome-wayland).

## 1. Objetivo

**Directriz de evolución del mantenedor (2026-10-31):** las próximas
implementaciones deben preservar experiencia y calidad aceptadas y limitarse a
mejoras y correcciones legítimas. Regresiones conocidas bloquean la integración;
cambios experimentales no pasan a ser predeterminados sin evidencia. Cada
incremento debe validar su alcance y comportamiento afectado y registrar lo no probado.

PixelPointer es una herramienta de presentación Native AOT (`pixelpointer`)
para Linux, Windows y macOS, con funcionalidades de ZoomIt (Sysinternals/PowerToys):
zoom de pantalla, anotación, temporizador de descanso, Live Zoom, recorte,
grabación y DemoType, activadas mediante atajos globales.

No pretende reproducir la marca, los iconos ni la interfaz de ZoomIt. Su código
en PowerToys es MIT y puede servir de referencia de comportamiento, con
atribución cuando se adapte algo.

## 2. Decisiones registradas

| # | Decisión | Valor |
| --- | --- | --- |
| D1 | Nombre del producto y del binario | PixelPointer / `pixelpointer`; app id `io.github.lzocateli.PixelPointer` |
| D2 | Licencia | MIT |
| D3 | Stack | .NET 10 Native AOT; SDL3 (ppy.SDL3-CS) para ventana y entrada; SkiaSharp para dibujo; Tmds.DBus.Protocol para portales XDG. Avalonia descartado: en Linux usa XWayland y el producto es principalmente una ventana de superposición |
| D4 | Wayland | X11 nativo → portal GlobalShortcuts → `pixelpointer hotkeys install` (atajo del escritorio llamando la CLI mediante IPC). Live Zoom y DemoType con soporte de mejor esfuerzo |
| D5 | macOS | Primera versión sin firmar; firma y notarización con Developer ID cuando esté disponible |
| D6 | MP4 en Linux | `ffmpeg` del sistema cuando exista (no incluido); GIF nativo en C# en las tres plataformas |
| D7 | Ubicación | `~/projetos/PixelPointer`, proceso y gobernanza como en DotnetScope |
| D8 | Threading (F0) | `Main` síncrono; SDL siempre en el hilo principal; trabajo asíncrono D-Bus/IPC en otros hilos, entregado por cola. Un bucle de ventana en la continuación de un `await` bloquea el hilo de lectura de D-Bus |
| D9 | Primer uso en GNOME (F0) | Permiso de captura y asociación de atajos requieren una ventana PixelPointer enfocada y el `.desktop` instalado con el app id; el instalador y la ventana de bienvenida lo gestionan |
| D10 | Señales (F0) | `SDL_HINT_NO_SIGNAL_HANDLERS=1`; SIGINT/SIGTERM gestionados mediante `PosixSignalRegistration` |
| D11 | Idiomas de documentación (2026-10-30) | pt-BR es la referencia (`README.md`, `docs/*.md`); traducciones en `README.en.md`, `README.es.md`, `docs/en/` y `docs/es/`. La regla vigente de `.github/instructions/documentation.instructions.md` también incluye planes e informes, sustituyendo el alcance anterior limitado a incorporación y uso |
| D12 | Paquetes y plataformas (2026-10-30) | Paquetes con ejecutable, SDL3, libSkiaSharp, icono SVG, `LICENSE` y `THIRD-PARTY-NOTICES.md`; instaladores crean directorio de aplicación y enlace del comando. Linux mínimo glibc 2.34 (requerido por libSDL3 incluida); sin `linux-musl` (SDL3-CS no publica binario musl) |
| D13 | ScreenCast en Wayland (2026-10-31, decisión del mantenedor) | Sesión **bajo demanda** por captura, cerrada tras el primer cuadro, para que el indicador de pantalla compartida no sea permanente; mantener la sesión abierta solo se evaluará con mediciones. `libpipewire-0.3` (MIT) del sistema mediante `LibraryImport`, sin redistribuirla. Restore token en `$XDG_STATE_HOME/pixelpointer` (0600, nunca en logs). Solo con un monitor; en otro caso, o ante fallos, portal Screenshot. La cancelación del usuario no se elude. **Revisión 2026-11-01 (decisión del mantenedor, opción A):** en GNOME la captura usa siempre el portal Screenshot, porque en GNOME 46 el indicador de pantalla compartida quedaba fijo tras ScreenCasts cortos; los demás compositores mantienen ScreenCast |

## 3. Arquitectura

El diagrama siguiente registra la arquitectura objetivo. La implementación actual
contiene `PixelPointer.App`, `PixelPointer.Core` y `PixelPointer.Rendering`;
las abstracciones/proyectos `PixelPointer.Platform*` indicados abajo aún no se
han extraído de la aplicación.

```text
src/
  PixelPointer.Core/              modelo de anotaciones, herramientas, undo/redo, configuración (JSON source-gen), comandos
  PixelPointer.Rendering/         Skia: trazos, formas, texto, blur, zoom/pan, temporizador; CPU y GPU (GL)
  PixelPointer.Platform/          interfaces: IHotkeyService, IScreenCapture, ILiveCapture, IClipboard, IInputInjector, IRecorder
  PixelPointer.Platform.Windows/  Win32 RegisterHotKey, DXGI/Windows.Graphics.Capture, Media Foundation, SendInput
  PixelPointer.Platform.MacOS/    Carbon RegisterEventHotKey, ScreenCaptureKit, AVFoundation, CGEventPost
  PixelPointer.Platform.Linux/    X11 (XGrabKey/XShm/XTest), portales (GlobalShortcuts/Screenshot/ScreenCast/RemoteDesktop), PipeWire
  PixelPointer.App/               ejecutable AOT: bandeja, superposición, configuración, CLI/IPC (concentra el código promovido del spike)
tests/
  PixelPointer.UnitTests/         xunit.v3 + Microsoft.Testing.Platform
  PixelPointer.IntegrationTests/  renderizado determinista comparado con imágenes de referencia
```

**Estado F2 (2026-11-04):** `src/PixelPointer.Core` contiene el modelo de
anotaciones (`AnnotationDocument`: trazos, color, grosor, pizarra, deshacer y
borrar), sin SDL ni Skia, cubierto por pruebas unitarias. La superposición usa este
modelo sin cambiar atajos ni apariencia. La configuración de solo lectura en
`settings.json` (color y grosor iniciales, zoom inicial; JSON con source
generator) se describe en la [guía de uso](uso.md#configuración).
`src/PixelPointer.Rendering` (`SceneRenderer`) dibuja fondo, pizarra y trazos
de forma independiente de la ventana, con pruebas de imagen en CPU; la
superposición conserva solo el cursor y los mensajes. La renderización en GPU
(Skia sobre el OpenGL de SDL) existe como opción experimental (`rendering.gpu`)
con retorno automático a CPU. En un contenedor con Xvfb y Mesa llvmpipe (GLX), el
zoom en OpenGL produjo píxeles idénticos a los de CPU; la interfaz OpenGL nativa
de Skia puede no estar disponible con EGL (Wayland), en cuyo caso se usa la CPU.
Falta la aceptación manual en hardware real y la
medición en 4K antes de hacerla predeterminada. En Wayland fuera de GNOME, la
captura usa ScreenCast + PipeWire bajo demanda (D13): 36 ms en Wayland aislado con
sway, frente a ~550 ms de Screenshot en GNOME en la F0. Captura, zoom y dibujo
tuvieron aceptación manual en GNOME real el 2026-10-31; el primer cuadro tardó
122 ms, pero el tiempo del portal incluyó consentimiento humano; no son
benchmarks equivalentes. Las funciones principales de captura, configuración,
modelo, renderizado y herramientas de anotación están implementadas. Los
criterios F2 restantes y la decisión sobre abstracciones de plataforma se
centralizan en la [sección 8](#8-pendientes-consolidados).

**Incremento de diagnóstico F2 (2026-10-31):** la captura informa tiempos de
creación de sesión, selección, inicio, persistencia de autorización, apertura del
remoto PipeWire y lectura/conversión. Conserva los totales `portal` y
`primeiro quadro`; este último incluye abrir el remoto, no solo leer. Solo
informa si se solicitó restauración, nunca el token. Consentimiento, timeout,
cierre y alternativa no cambian. Pruebas deterministas cubren el formato;
estos tiempos no incluyen todos los pasos de atajo a presentación.
[Interpretación](uso.md) y benchmark en GNOME real siguen separados; este
diagnóstico no completa F2.

**Consulta agrupada de capacidades (F2, 2026-10-31):** versión, tipos de fuente
y modos del cursor se leen mediante `Properties.GetAll`, reduciendo tres
lecturas individuales a una cuando la respuesta es compatible. No se guardan
valores en cache. Respuestas ausentes, incompletas o no soportadas conservan
la ruta `Get` anterior en esa conexión con su orden y salidas anticipadas;
la comprobación incompatible inicial cuesta una llamada adicional.
`ScreenCast capacidades` informa tiempo y método usados. Las pruebas cubren
tipos, propiedades ausentes, soporte de monitor, lectura anterior y cancelación.
Consentimiento y captura no cambian; benchmark y matriz de compatibilidad pendientes.

**Compatibilidad bajo rechazo controlado (2026-11-01):** el orquestador
`Test-WaylandScreenCast.ps1` acepta `-DenyScreenCastGetAll` e inyecta una política
obligatoria solo en el D-Bus descartable. El escenario normal pasó con `GetAll`;
el rechazo confirmó dos negativas del bus, lecturas `Get`, dos capturas mediante
portales reales ScreenCast/PipeWire y un cuadro actualizado. No cambió código
de producción, portales ni consentimiento. No significa que el portal rechace
naturalmente `GetAll`: es inyección de fallo auditada. GNOME/KDE y respuestas
naturalmente incompatibles siguen pendientes. La repetición en una conexión
se validó en el escenario aislado siguiente.
[Evidencias y comando](plano-e2e-grafico-uv.md#compatibilidad-getall-bajo-rechazo-controlado).

**Repetición en el daemon (2026-11-01):** `-RepeatDaemonCaptures` inició un
daemon y ejercitó dibujo y zoom mediante IPC. El monitor D-Bus, limitado a
llamadas de propiedades, confirmó capacidades actuales: dos `GetAll` en el
control o una comprobación rechazada seguida de seis `Get` ordenados, sin
otro intento `GetAll` en la segunda sesión. Cambiaron colores del cuadro,
el daemon siguió activo tras cerrar ventanas mediante el compositor y `quit`
terminó normalmente. No cambió código de producción. No comprueba atajos
físicos, Esc, bandeja, quit con superposición abierta ni la matriz GNOME/KDE.
[Registro](plano-e2e-grafico-uv.md#capturas-repetidas-en-la-conexión-del-daemon).

El mismo binario es aplicación y CLI: `pixelpointer zoom|draw|timer <min>|snip`
envía el comando mediante socket Unix (`$XDG_RUNTIME_DIR` o directorio temporal;
AF_UNIX también en Windows 10+) a la instancia en ejecución.

## 4. Matriz de capacidades por plataforma

| Capacidad | Windows | macOS | Linux X11 | Linux Wayland |
| --- | --- | --- | --- | --- |
| Atajo global | RegisterHotKey | RegisterEventHotKey | XGrabKey | Portal GlobalShortcuts (**validado en GNOME 50**); alternativa de atajo del escritorio + IPC |
| Captura estática | GDI | ScreenCaptureKit (permiso de grabación de pantalla) | XGetImage/XShm | Screenshot en GNOME; ScreenCast/PipeWire bajo demanda en otros compositores con un monitor; Screenshot para varios monitores o como alternativa |
| Superposición a pantalla completa | SDL3 | SDL3 + nivel de ventana | SDL3 | SDL3 Wayland nativo (**validado**, escala fraccionaria funcional) |
| Live Zoom | Implementado con Magnification API | Implementado mediante ScreenCaptureKit | Implementado como lupa en ventana | Mejor esfuerzo mediante ScreenCast y metadatos del cursor; un monitor, portal compatible, deshabilitado en GNOME por D13 |
| Grabación | GIF implementado | No implementada | GIF implementado | GIF implementado mediante sesión ScreenCast |
| DemoType | No implementado | No implementado | No implementado | No implementado; RemoteDesktop/libei sigue siendo una posibilidad, no soporte existente |

## 5. Fases

Las fases siguientes resumen el avance; el trabajo accionable y los criterios
de validación aparecen exclusivamente en la [sección 8](#8-pendientes-consolidados).

| Fase | Estado | Resumen |
| --- | --- | --- |
| **F0 — Spike** | Completada | Informe go/no-go ([pt-BR](../f0-relatorio.md)) |
| F1 — Fundación | Completada | Gobernanza, CI, releases y documentación inicial entregadas |
| F2 — Core + captura rápida | Parcial | Core, captura y configuración implementados; criterios restantes en la sección 8 |
| F3 — Zoom + Dibujo | Parcial | Herramientas principales implementadas; cobertura Wayland/plataformas incompleta |
| F4 — Temporizador + Recorte | Implementada | Aceptación manual local GNOME/X11 el 2026-11-04 |
| F5 — Live Zoom | Parcial | Windows/macOS/X11 implementados; Wayland con mejor esfuerzo |
| F6 — Grabación | Parcial | GIF implementado en Windows y Linux X11/Wayland |
| F7 — DemoType | No iniciada | Aún no implementada |
| F8 — Distribución | Parcial | Canales actuales entregados; formatos adicionales esperan decisión de alcance |
| F9 — i18n y documentación | Parcial | Documentación actual pt-BR/en/es; faltan traducciones de documentos anteriores |

## 6. Riesgos

Los riesgos activos y sus próximas acciones se registran en la tabla única de
la [sección 8](#8-pendientes-consolidados); este plan no mantiene una lista paralela.

## 7. Preguntas abiertas

Las decisiones de alcance y el trabajo pendiente se siguen únicamente en la
[sección 8](#8-pendientes-consolidados).

## 8. Pendientes consolidados

**Prueba manual en X11 con la preview `v0.1.3-preview.25` (2026-11-03):** se probaron dibujo, zoom, temporizador, recorte, Live Zoom y grabación GIF (evidencias adjuntas por el mantenedor). El mantenedor aclaró que iniciar Live Zoom en la posición del mouse ya se cumple al centrar la región capturada en el cursor; el comentario no requiere cambiar el comportamiento. La prueba de grabación no constituye aceptación formal.

Esta es la fuente única de estado y próximas acciones para las pendientes de
implementación, validación, decisión y documentación. Los planes E2E y los
registros de aceptación conservan evidencias y límites históricos, sin mantener
otra lista de tareas. Actualizado el 2026-11-04; los PR históricos están en el
repositorio y los resultados gráficos en el [plan E2E](plano-e2e-grafico-uv.md).

| ID | Tipo | Estado | Pendiente y criterio de cierre |
| --- | --- | --- |
| IMPL-01 | Implementación | Parcial | Implementar captura/selección de monitor en Wayland preservando la política Screenshot de GNOME; comprobar selección de salida y recorte en un compositor real |
| IMPL-02 | Implementación | Pendiente | Hacer asíncrono el inicio de grabación Wayland; el consentimiento, la espera del portal y los errores no deben bloquear callbacks del daemon |
| IMPL-03 | Implementación | No implementada | Definir e implementar GIF en macOS, MP4 y audio solo después de seleccionar componentes con licencias permisivas verificadas; FFmpeg no está aprobado |
| IMPL-04 | Implementación | No iniciada | Diseñar e implementar DemoType por plataforma, incluidos permisos y límites de inyección de entrada en Wayland |
| IMPL-05 | Implementación/arquitectura | No extraída | Decidir si siguen siendo necesarias las abstracciones `PixelPointer.Platform*`; si lo son, extraerlas de `PixelPointer.App` sin duplicar adaptadores actuales |
| IMPL-06 | Implementación | Implementada; pendiente de revisión humana del [ADR 2026-0001](adr/2026-0001-atualizacao-opt-in.md) y validación en Windows y macOS | Búsqueda e instalación automática optativa para instalaciones autogestionadas (`pixelpointer update`, submenú **Atualizações**); `.deb`, Homebrew y winget solo avisan y dirigen al usuario a su gestor de paquetes. Validado en Linux x64 con pruebas unitarias, instaladores, E2E X11 y una actualización real desde GitHub. Requisitos y decisiones en el [plan de actualización](plano-atualizacao.md) |
| DEC-01 | Decisión de alcance | Pendiente | Decidir si `.zip`, `.dmg`, AppImage y Flatpak siguen en alcance; no implementar inicio automático sin aprobación explícita |
| DOC-01 | Documentación | Pendiente | Crear traducciones en/es del informe F0 y del plan histórico de la issue #3 |
| VAL-01 | Validación de rendimiento | Meta no alcanzada | Medir y tratar la latencia GNOME: captura observada de 520–531 ms y primer cuadro de 585–635 ms frente a la meta < 150 ms; registrar una decisión si cambia la meta |
| VAL-02 | Validación de rendimiento | Pendiente | Medir el renderizador GPU en hardware real y 4K; decidir con esos resultados si deja de ser optativo |
| VAL-03 | Validación nativa | Pendiente | Definir una ejecución compatible con las reglas de contenedores (o conseguir autorización explícita para una excepción) y completar el recorrido Windows de captura, atajos, GIF, Ctrl+S, bandeja y salida; registrar SO/arquitectura y evidencias |
| VAL-04 | Validación nativa | Pendiente | Definir una ejecución compatible con las reglas de contenedores (o conseguir autorización explícita para una excepción) y validar en macOS captura, Live Zoom, permisos, instalación y atajos; no incluir grabación hasta implementarla |
| VAL-05 | Compatibilidad/E2E | Parcial | Ejecutar matriz Wayland en GNOME/KDE y otros compositores: incompatibilidad natural de portales, consentimiento real, atajos físicos y bandeja. Sway aislado ya cubre ScreenCast, grabación, ciclo de vida, rechazo controlado de `GetAll` y escalas 1/1,5/2 |
| VAL-06 | Hardware/E2E | Parcial | Validar conflictos de atajos, lápiz/presión, varios monitores, escalas y GPU en hardware real; X11/Windows y GNOME/X11 con dos monitores tienen evidencia local, no cobertura completa |
| VAL-07 | Compatibilidad | Pendiente | Ejecutar y registrar pruebas Linux arm64 en hardware adecuado |

**Evidencia parcial en Windows el 2026-10-31:** el solicitante adjuntó cuatro
capturas en esta conversación. Muestran la instalación con `install.ps1` y la
verificación del SHA-256, el daemon registrando Ctrl+1 y Ctrl+2, el menú de la
bandeja con Zoom, Desenhar y Sair, y anotaciones sobre la pantalla. También
muestran dos problemas de orientación, ya documentados en el
[README](../../README.es.md#instalación): `PATH` desactualizado en la sesión
actual de PowerShell y `profile.ps1` bloqueado por la directiva de ejecución
`Restricted`. Las capturas no identifican la versión de Windows ni la
arquitectura y no prueban zoom, Ctrl+S con Known Folder, salida mediante Sair ni
ejecución en segundo plano. Los adjuntos permanecen en la conversación, sin
copia al repositorio.
