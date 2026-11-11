# E2E gráfico X11 aislado

[Português (Brasil)](README.md) · [English](README.en.md) · **Español**

Implementación del primer objetivo del [plan](../../docs/es/plano-e2e-grafico-uv.md).
Ejercita el paquete Native AOT mediante entrada XTEST, píxeles del escritorio,
IPC público y un panel StatusNotifier sintético. No accede a la sesión gráfica
del desarrollador.

## Ejecutar

Ejecute desde la raíz del repositorio en Linux x64, con PowerShell 7 y Podman o
Docker. Las compilaciones necesitan acceso al registry, NuGet, PyPI y al snapshot
de Debian. La prueba gráfica se ejecuta sin acceso externo. Docker está previsto
en la interfaz, pero esta entrega solo se verificó con Podman rootless.

```powershell
pwsh -NoProfile -File tools/scripts/Publish-Aot.ps1 -Rid linux-x64 -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -Build -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -Repeat 10 -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -NoCompositor -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -NoTray -Engine podman
```

`Publish-Aot.ps1` produce `artifacts/package/linux-x64`. `-Package` acepta solo
un directorio dentro de `artifacts`, sin enlaces simbólicos. `-Build` construye
la imagen local; si no se usa, la imagen debe existir. Cada ejecución fija el
ID de la imagen inspeccionada para todos los intentos, sin reconstrucciones
intermedias. `--help` no inicia contenedores. No ejecute el runner directamente
en el host.

El resultado normal es el código cero. Los fallos, la falta de evidencias o los
timeouts devuelven un código distinto de cero; el primer intento reprobado
interrumpe la secuencia. Para comprobar la detección de fallos:

```powershell
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -InjectFailure -Engine podman
```

Este comando **debe fallar** con código 1: el runner mata solo el daemon hijo y
exige que siga disponible en la siguiente comprobación. El informe debe registrar
`Daemon interrompido: -9`, nunca convertir ese fallo en éxito del escenario.

## Cobertura y referencias

La matriz funcional está en el [plan](../../docs/es/plano-e2e-grafico-uv.md#5-matriz-de-escenarios-de-funcionalidades-actuales).
El runner comprueba foco externo, atajos, modificadores, conflictos reales de
grab, zoom, colores, grosor, edición, pizarras, transparencia, guardado, errores
visibles, orden de logs de diagnóstico de teclas/botones, bandeja, reinicio,
cierre con ventana abierta e instalación/apertura/eliminación del lanzador del
escritorio. También comprueba el recorte con Ctrl+6 y PNG de 160x100,
y la grabación con Ctrl+5 y GIF de 800x600 con varios cuadros. El escritorio
virtual tiene un monitor; la selección entre monitores requiere aceptación
local en una topología real. La consulta X11 debe reflejar la nueva posición
del cursor antes de cada operación. El lanzador interpreta el archivo
desktop instalado, pero solo acepta `/package/pixelpointer daemon`, sin shell y
con `Terminal=false`.

Las referencias usan regiones y colores explícitos en el runner, no baselines
actualizadas automáticamente. Ejemplos: escena cian RGB `(0,170,204)`, anchos
de zoom `280/350/438` con tolerancia de 4 píxeles, colores de anotación con
tolerancia de 8 canales y área mínima, cambios del fondo animado con trazos
persistentes y PNG opacos con la pantalla capturada y los trazos, con o sin
compositor. Los PNG se decodifican.

Geometría fija: 800x600, escala 1, renderizado SDL por software, locale `C.UTF-8`,
UTC y `xcompmgr -n`. La imagen no tiene GLX/Mesa. Con compositor, el runner
selecciona explícitamente el visual TrueColor de 32 bits mediante el hint público
`SDL_VIDEO_X11_WINDOW_VISUALID`; sin compositor, usa el visual predeterminado
de 24 bits. Esto demuestra el dibujo transparente en esta configuración, **no**
la selección automática del visual ni paridad con GNOME, una GPU o un escritorio
completo.

## Evidencias y retención

Los resultados se guardan sin sobrescribir en
`artifacts/e2e/x11-<UTC>-<id>/attempt-NN`:

| Archivo | Contenido |
| --- | --- |
| `desktop.avi` | Captura real del escritorio con XGetImage, MJPEG a 5 FPS, sin audio |
| `*.png` | Capturas, captura CLI y exportaciones reales de Ctrl+S |
| `timeline.jsonl` | Acciones y comprobaciones con tiempo monotónico creciente |
| `result.json`, `junit.xml` | Resultado, duración y motivo del fallo |
| `manifest.json` | SHA-256 de cada archivo del paquete, ID de imagen, entorno y vídeo |
| `native-components.tsv`, `native-packages.tsv` | Fuentes compiladas y paquetes Debian exactos |
| `native-links.txt`, `python-packages.json` | Enlaces nativos resueltos y dependencias Python instaladas |

La grabación comienza antes de la primera acción del producto. Cada captura
espera un cuadro posterior para continuar; el cierre también exige un cuadro
posterior a la última acción. Se decodifican todos los JPEG, con resolución
obligatoria y tolerancia de duración de 0,4 segundos. Un fallo de grabación
reprueba el resultado. El vídeo no sustituye las aserciones objetivas.

Los artefactos no se publican, rotan ni eliminan automáticamente. Conserve las
ejecuciones reprobadas hasta terminar el análisis; conserve la secuencia de
estabilidad y los fallos controlados hasta la revisión humana. La eliminación
posterior es manual y se limita al ID elegido explícitamente. No use prune global.

## Aislamiento y amenazas

| Superficie | Control y riesgo residual |
| --- | --- |
| Captura y entrada | Solo Xvfb privado; no se montan display, D-Bus, home ni dispositivos de entrada del host |
| Privilegios | UID 1000, capabilities eliminadas, no-new-privileges, filesystem de solo lectura y tmpfs privado |
| Red | Red interna, sin puertos publicados; nginx separado y smoke HTTP 404; no hay auxiliares HTTP directos |
| Paquete | Montaje de solo lectura y rutas normalizadas sin symlinks; ejecutar solo un paquete local confiable |
| Escritura | Volumen exclusivo para evidencias y temporales descartables; sin secrets ni perfiles reales |
| Procesos | Argumentos conocidos, sin shell en runner/lanzador, terminar solo hijos y recursos identificados |
| Recursos | Runner: 2 CPUs, 2 GiB, 128 procesos y tmpfs de 512 MiB; nginx: 128 MiB y 32 procesos |
| Diagnóstico | Solo tokens conocidos y mensajes de prueba; no recopilar logs completos ni todo el entorno |

X11 se inicia con `-ac`, aceptable solo dentro de este contenedor sin socket ni
puerto expuestos. La red interna y el sandbox no hacen seguro ejecutar un paquete
hostil contra el kernel del host. La imagen y el paquete deben ser confiables. El
volumen de evidencias es solo un canal de salida de la prueba, no un lugar para
datos personales. Ningún permiso de portal/TCC se simula como consentimiento real.
En el escenario con compositor, el escritorio descartable proporciona un mock
local de `org.freedesktop.portal.Screenshot`, que rechaza solicitudes con código
`2`. La prueba exige que Ctrl+S use `XGetImage` sin llamar a ese portal y verifica
el fondo capturado, el recorte por monitor y las anotaciones en el PNG. Esto no
verifica la captura en el compositor real de GNOME ni la autorización de Wayland.

El plazo cooperativo es de 120 segundos; SIGALRM a los 150 segundos genera un
error para finalizar evidencias; el orquestador mata el contenedor a los 180
segundos. SIGKILL o un fallo del engine pueden impedir finalizar el AVI; eso no
es aprobación.

## Diagnóstico y recuperación

1. Lea `result.json` y la última comprobación en `timeline.jsonl`; compare el PNG
   de fallo y el vídeo. Los fallos de infraestructura también reprueban.
2. Si falla la compilación, compruebe el acceso al digest/snapshot y el gate de
   enlaces nativos. No sustituya versiones ni relaje licencias automáticamente.
3. Si falla la exportación o hay timeout externo, el script informa del volumen
   conservado. Recupérelo en un contenedor auxiliar de la misma imagen, de solo
   lectura y sin red:
   `podman create --name <auxiliar-unico> --network none --read-only --volume
   <volumen-informado>:/evidence:ro --entrypoint python
   localhost/pixelpointer-e2e:x11-v1 --version`, y use después
   `podman cp <auxiliar-unico>:/evidence/results/. <directorio-nuevo>`. Elimine
   solo ese auxiliar y, tras comprobar la copia, el volumen identificado.
4. Si faltan el informe o los cuadros, mantenga la ejecución reprobada. Una nueva
   ejecución recibe otro ID y no borra la anterior.
5. Escale diferencias entre el escritorio sintético y el real a la lista manual
   y al responsable del backend; no monte la sesión del host para saltarse el
   aislamiento.

## Dependencias y licencias

Herramientas exclusivas de prueba, no añadidas al paquete distribuido. Verificadas
localmente el 2026-10-30 con avisos de artefactos exactos, metadatos de PyPI y
snapshot de Debian. Python y su manifiesto/lock están fijados; el build ejecuta
`uv sync --locked` y el runtime offline usa `uv run --locked --no-sync`, sin
actualizaciones ni descargas automáticas.

| Componente | Versión fijada | Licencia elegida y fuente |
| --- | --- | --- |
| Python | 3.13.17 | PSF-2.0; imagen oficial por digest en Containerfile |
| uv | 0.13.6 | MIT, alternativa Apache-2.0; [release PyPI](https://pypi.org/project/uv/0.13.6/) |
| Pillow | 13.0.0 | MIT-CMU y avisos transitivos del wheel; [release PyPI](https://pypi.org/project/pillow/13.0.0/) |
| dbus-fast | 5.2.0 | MIT; [release PyPI](https://pypi.org/project/dbus-fast/5.2.0/) |
| Xvfb | 2:21.1.16-1.3+deb13u4 | MIT/X11 y avisos permisivos de `COPYING`; fuente Debian xorg-server |
| D-Bus | 1.16.2-2 | AFL-2.1, **no** la alternativa GPL; herramientas GPL deshabilitadas |
| twm | 1:1.0.10-1+b1 | MIT/X11/Open Group, avisos en `/opt/twm/copyright` |
| xcompmgr | 1.1.8-1 | XFree86 permisiva; copyright Debian instalado |
| nginx | 1.26.3-3+deb13u10 | BSD-2-Clause, `LICENSE` de la fuente compilada |

Fuentes Debian: [main](https://snapshot.debian.org/archive/debian/20261029T000000Z/)
y [security](https://snapshot.debian.org/archive/debian-security/20261029T000000Z/).
Releases PyPI consultadas: uv el 2026-10-27, Pillow el 2026-10-16 y dbus-fast
el 2026-10-02; Xorg/nginx incluyen las revisiones de seguridad fijadas arriba.
Actualizar requiere verificar de nuevo mantenimiento, avisos, lock, build y matriz.

Los wheels no agregan dependencias Python transitivas. El wheel Pillow incluye
bibliotecas nativas y sus avisos en
`pillow-13.0.0.dist-info/licenses/LICENSE`: AOM/dav1d/libavif (BSD),
Brotli/HarfBuzz (MIT), bzip2, libjpeg (IJG), lcms2 (MIT), liblzma (0BSD),
libpng (libpng), libtiff (libtiff), libwebp/libyuv (BSD), OpenJPEG (BSD),
libraqm (MIT), Xau/XCB/Xdmcp (MIT/X11), zlib y zstd (BSD). FreeType usa FTL,
no GPL; zstd usa BSD, no GPL. Prevalecen los avisos completos del wheel; no se
incluyen FFmpeg ni FriBidi.

Xvfb y D-Bus se compilan sin libsystemd, libaudit, libapparmor, libunwind y GLX;
nginx sin auth_basic, gzip, rewrite y PCRE. El gate `ldd` rechaza enlaces ausentes
y familias opcionales incompatibles, como nettle, libelf y libcrypt. X11, pixman,
expat, fontconfig/FreeType y OpenSSL usan alternativas permisivas; las versiones
transitivas se fijan en el snapshot y se inventarían en cada ejecución.
`/opt/native-notices` guarda avisos de las fuentes compiladas. El inventario
Debian también enumera herramientas y componentes preexistentes del sistema
base; eso no significa que toda la distribución o toolchain tenga licencia MIT.
La imagen de prueba no es un nuevo artefacto de distribución de PixelPointer.

## Límites de aceptación

La suite automatizada solo se ejecutó en Linux x64/X11 virtual con twm y panel
sintético. El solicitante confirmó por separado el dibujo transparente, la
selección automática del visual, zoom, exportación con alfa, CapsLock/NumLock y
uso en ambos monitores 1920x1080 a escala 1 en GNOME/X11 real, autorizando cerrar
esas pruebas. El [registro de aceptación manual](../../docs/es/plano-e2e-grafico-uv.md#aceptación-manual-del-dibujo-transparente-en-gnome-x11)
contiene el entorno, el hash del PNG y los límites. La matriz GNOME completa,
Wayland/portales, arm64, Windows/macOS, otras GPU, lápiz, escala fraccionaria y
otras topologías de monitores siguen sin verificarse. La revisión humana de los
primeros vídeos y referencias de la suite sigue pendiente; los resultados
automatizados no conceden esa aceptación.
