# Plan de implementación — PixelPointer

[Português (Brasil)](../plano-implementacao.md) · [English](../en/plano-implementacao.md) · **Español**

| Campo | Valor |
| --- | --- |
| Estado inicial | Aprobado para F0 (decisiones del 2026-10-29) |
| Estado de entrega | F0 validada en Linux/GNOME Wayland, incluido Native AOT; Windows con evidencia manual parcial el 2026-10-31 y macOS pendiente del recorrido manual ([informe, pt-BR](../f0-relatorio.md), [pendientes](#8-pendientes-consolidados)). F1 (repositorio, gobernanza, CI/CD y distribución) entregada el 2026-10-30 |
| Primera versión funcional | v0.1.2, aceptada como estable por el mantenedor el 2026-10-30 en Ubuntu 24.04.5/GNOME X11 y Ubuntu 26.04/GNOME Wayland; aceptación manual limitada a flujos/entornos registrados, sin completar F2/F3 ni validar Windows/macOS |
| Origen | Asistido por IA |
| Revisor humano | Lincoln Zocateli |
| Repositorio | <https://github.com/lzocateli/PixelPointer> |
| Licencia | MIT |

**Aceptación complementaria del 2026-10-30:** el solicitante confirmó dibujo
transparente, zoom, exportación PNG con alfa, CapsLock/NumLock y uso en ambos
monitores 1920×1080 con escala 1 en Ubuntu 24.04.5/GNOME X11, autorizando dar
esas pruebas por concluidas. El [registro de evidencias y límites](plano-e2e-grafico-uv.md#aceptaci%C3%B3n-manual-del-dibujo-transparente-en-gnome-x11)
separa la aceptación manual del E2E automatizado. No completa toda la fase F3:
formas, texto, blur, HiDPI y paridad entre plataformas siguen como alcance futuro,
al igual que la aceptación real de Zoom/Desenhar desde la bandeja, del lanzador
y la revisión de vídeos automatizados. Capturas complementarias de la misma fecha
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
| D13 | ScreenCast en Wayland (2026-10-31, decisión del mantenedor) | Sesión **bajo demanda** por captura, cerrada tras el primer cuadro, para que el indicador de pantalla compartida no sea permanente; mantener la sesión abierta solo se evaluará con mediciones. `libpipewire-0.3` (MIT) del sistema mediante `LibraryImport`, sin redistribuirla. Restore token en `$XDG_STATE_HOME/pixelpointer` (0600, nunca en logs). Solo con un monitor; en otro caso, o ante fallos, portal Screenshot. La cancelación del usuario no se elude |

## 3. Arquitectura

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

**F2 en curso (2026-10-31):** `src/PixelPointer.Core` ya contiene el modelo de
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
medición en 4K antes de hacerla predeterminada. En Wayland con un monitor, la
captura usa ScreenCast + PipeWire bajo demanda (D13): 36 ms en Wayland aislado con
sway, frente a ~550 ms de Screenshot en GNOME en la F0. Captura, zoom y dibujo
tuvieron aceptación manual en GNOME real el 2026-10-31; el primer cuadro tardó
122 ms, pero el tiempo del portal incluyó consentimiento humano. Falta medir
latencia de atajo a cuadro sin ese diálogo; no son benchmarks equivalentes. Formas, texto, atajos
configurables y los demás módulos anteriores siguen pendientes.

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
| Captura estática | DXGI/WGC | ScreenCaptureKit (permiso de grabación de pantalla) | XShm | ScreenCast/PipeWire bajo demanda con un monitor (aceptación manual de preview); alternativa Screenshot |
| Superposición a pantalla completa | SDL3 | SDL3 + nivel de ventana | SDL3 | SDL3 Wayland nativo (**validado**, escala fraccionaria funcional) |
| Live Zoom | Magnification API o WGC | ScreenCaptureKit excluyendo la propia ventana | XComposite (complejo) | ScreenCast + layer-shell donde exista; GNOME con mejor esfuerzo |
| Grabación | Media Foundation | AVFoundation | ffmpeg del sistema / GIF | ScreenCast + ffmpeg / GIF |
| DemoType | SendInput | CGEventPost (Accesibilidad) | XTest | Portal RemoteDesktop/libei, mejor esfuerzo |

## 5. Fases

| Fase | Entrega | Criterio de salida |
| --- | --- | --- |
| **F0 — Spike** | SDL3+Skia+AOT, captura, atajos, IPC por plataforma | Informe go/no-go ([pt-BR](../f0-relatorio.md)) |
| F1 — Fundación | Repositorio público, Altivy.Groundwork (gobernanza, release-please, gitleaks, política), matriz CI, publicación de 6 RIDs con SBOM y atestaciones, `.deb`, instaladores, Homebrew y winget, documentación pt/en/es; spike pasa a `src/PixelPointer.App` | CI verde en los 3 SO |
| F2 — Core + captura rápida | Modelo de anotaciones, Skia en **GPU**, configuración, abstracciones de plataforma, IPC/CLI, **ScreenCast/PipeWire en Wayland** (meta: atajo → primer cuadro < 150 ms), ScreenCaptureKit en macOS, bienvenida (D9) | Pruebas unitarias y de imagen; latencia medida |
| F3 — Zoom + Dibujo | Ctrl+1/Ctrl+2 completos: formas (línea, flecha, rectángulo, elipse), resaltador, blur, texto, multimonitor, HiDPI, copiar/guardar | Paridad funcional validada manualmente |
| F4 — Temporizador + Recorte | Ctrl+3 y Ctrl+6 (portapapeles y archivo) | — |
| F5 — Live Zoom | Windows → macOS → X11; Wayland con mejor esfuerzo | Matriz documentada |
| F6 — Grabación | GIF nativo; MP4 por plataforma; ffmpeg opcional en Linux; audio | — |
| F7 — DemoType | Windows, macOS, X11; Wayland mediante portal | — |
| F8 — Distribución | winget/zip, `.dmg` (sin firmar en v1) + Homebrew cask, `.deb`/AppImage/Flatpak, inicio automático, `.desktop` | Instalación limpia en los 3 SO |
| F9 — i18n y documentación | pt-BR y en, guía de permisos por SO | — |

## 6. Riesgos

| Riesgo | Mitigación |
| --- | --- |
| Latencia de captura Wayland (~550 ms por Screenshot) | ScreenCast con PipeWire y restore token en F2 |
| Dibujo CPU insuficiente en 4K | Skia sobre OpenGL (GRContext) en F2; CPU como alternativa |
| GNOME exige ventana enfocada para permisos | Ventana de bienvenida (D9) |
| macOS sin firma: permisos TCC perdidos entre versiones | Documentar; firmar cuando exista Developer ID (D5) |
| Portales diferentes en KDE/Hyprland/Sway | Probar en F2; alternativa IPC siempre disponible |
| Bibliotecas SDL3/libSkiaSharp junto al ejecutable | Incluirlas; evaluar enlace estático en F8 |

## 7. Preguntas abiertas

1. Multimonitor en Wayland: la captura cubre todo el escritorio y el compositor
   decide el monitor de pantalla completa; seleccionar el monitor del cursor
   requiere ScreenCast por monitor (F2).
2. Tamaño AOT en Windows/macOS (Linux: 19,2 MB con SDL3 y Skia; inicio < 10 ms).
3. Comportamiento Windows/macOS (pendiente del recorrido manual, sección 6 del informe).

## 8. Pendientes consolidados

**Secuencia solicitada el 2026-11-01:** cada etapa requiere rama propia,
pruebas antes del commit, push y PR; los resultados en container no sustituyen
aprobación humana ni validación nativa. La primera entrega consolida los
incrementos locales en `perf/f2-wayland-capture`.

| Etapa | Estado de ejecución |
| --- | --- |
| 1. Consolidar diagnóstico, capacidades y pruebas Wayland | [PR #21](https://github.com/lzocateli/PixelPointer/pull/21) integrado en `df252e6`, con `Result` aprobado |
| 2. Nueva preview y aceptación GNOME real | [Preview #16](https://github.com/lzocateli/PixelPointer/actions/runs/41308984865): builds, smokes y atestaciones aprobados; espera aprobación del Environment `release` y prueba humana. No concluida |
| 3. Ciclo de vida completo e interacción física | [PR #22](https://github.com/lzocateli/PixelPointer/pull/22) integrado en `0ad417f`, con `Result` aprobado; atajos físicos, Esc, bandeja y lanzador pendientes |
| 4. Matriz de compatibilidad | [PR #23](https://github.com/lzocateli/PixelPointer/pull/23) integrado en `05b0c40`. En `test/wayland-output-scale` se separó el bloqueo del fixture; corrección Wayland y escalas 1/1,5/2 aprobadas en prueba aislada; PR en preparación. Entornos reales, hardware y arquitecturas pendientes |
| 5. Completar F2 | GPU/4K, abstracciones, ScreenCaptureKit y bienvenida pendientes; no concluida |
| 6. Herramientas avanzadas F3 | Aún no implementadas |
| 7. F4–F7 | Temporizador, recorte, Live Zoom, grabación y DemoType aún no implementados |
| 8. F8–F9 | Distribución adicional y traducciones anteriores pendientes |

Actualizado el 2026-10-31. El [plan de la issue #3, pt-BR](../plano-correcao-issue-3.md)
se cerró junto con la issue y se conserva como registro histórico. Sus ítems
abiertos y los del [plan de E2E](plano-e2e-grafico-uv.md) se siguen aquí, además
de las fases F2 a F9 de la sección 5.

| Pendiente | Origen | Situación |
| --- | --- | --- |
| Aceptación manual de Zoom y Desenhar accionados desde la bandeja en GNOME X11 | Issue #3, 3.5 | Icono, menú y Sair aceptados; ambas acciones aún sin confirmar |
| Apertura desde el menú de aplicaciones de GNOME sin terminal | Issue #3, 3.6 | Aprobó en el E2E con lanzador sintético; falta aceptación real |
| Matriz Wayland por compositor: portales con y sin Registry, permisos denegados, bandeja y E2E aislado con libei/EIS | Issue #3, 3.1, 3.3 y 5; E2E, 8 | ScreenCast/PipeWire pasó en Sway aislado y captura, zoom y dibujo tuvieron aceptación manual con v0.1.3-preview.14 en Ubuntu 26.04.1/GNOME Wayland. Consentimiento observado; reutilización del token, duración del indicador, latencia sin diálogo, varios monitores y otros compositores siguen pendientes |
| Ctrl+S nativo: Known Folder en Windows, incluido OneDrive, y preferencia de macOS, con filesystem real por SO | Issue #3, 3.7 | Cerrado por decisión del mantenedor el 2026-10-31; lógica cubierta por pruebas, sin validación nativa registrada |
| Recorrido manual completo en Windows | F0; issue #3 | Evidencia parcial el 2026-10-31, descrita abajo |
| Recorrido manual completo en macOS | F0; issue #3 | Sin evidencia nativa |
| Revisión humana de los vídeos y referencias del E2E X11 | E2E, 6 y 8 | Cerrado por decisión del mantenedor el 2026-10-31, sin revisión registrada |
| Conflicto físico de atajos con otra aplicación, escala fraccionaria, otras topologías y GPU, lápiz | Issue #3, 3.2 y 3.3; E2E, 8 | Pendiente |
| Linux arm64 en arquitectura adecuada | E2E, 8 | Pendiente |
| Completar traducciones de documentos autorales anteriores | F9; regla de documentación multilingüe | [Informe F0](../f0-relatorio.md) y plan histórico de la issue #3 aún sin versiones en/es; README y guías actuales traducidos no completan esa cobertura |

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
