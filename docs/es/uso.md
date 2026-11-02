# Guía de uso

[Português (Brasil)](../uso.md) · [English](../en/usage.md) · **Español**

> Traducción de la referencia en portugués [docs/uso.md](../uso.md). Si
> difieren, prevalecen la versión en portugués y el comportamiento implementado.

**Versión funcional de referencia:** `v0.1.2`, primera versión funcional aceptada
como estable por el mantenedor el 2026-10-30 en Ubuntu 24.04.5/GNOME X11 y Ubuntu
26.04/GNOME Wayland. Aceptación manual limitada a flujos ejercitados;
Windows/macOS, otros compositores y la matriz completa siguen pendientes.
Consulte el [registro de evidencias](plano-e2e-grafico-uv.md). El informe F0 es
histórico; esta aceptación no cambia los límites de los backends descritos abajo.

## Primer uso opcional

Ejecuta `pixelpointer setup` en una sesión gráfica para abrir primer uso.
Es explícito: no aparece automáticamente con el daemon ni sustituye comandos
existentes. Abrir, cerrar o pulsar Esc no registra la aplicación, solicita
captura ni inicia el daemon. “Fechar” está seleccionado inicialmente.

- “Verificar ambiente” ejecuta el diagnóstico existente; detalles en la terminal.
- “Registrar aplicativo no desktop” solo se ofrece en Linux y conserva conflictos.
- “Solicitar captura de tela” solo se ofrece en Linux/Wayland y abre `grant`,
  con confirmación en ventana y portal. “Solicitação finalizada” no significa
  permiso concedido; consulta salida del comando y decisión del escritorio.
- “Iniciar daemon” solo se ofrece sin daemon activo. Cierra la ventana y el
  daemon sigue en primer plano como en el comando existente.

Selecciona por clic o Tab/flechas y Enter. No modifica PATH, perfiles, inicio
automático ni configuración, ni escribe marcador de primer uso. Registro y
permisos requieren tu elección explícita. Cerrar setup no detiene el daemon
existente. Ejecuta desde terminal para ver detalles y errores. Los comandos
visibles permanecen en portugués como otros mensajes nativos de la aplicación.

## Comandos

| Comando | Función |
| --- | --- |
| `pixelpointer daemon` | Se mantiene en ejecución, registra los atajos globales (Ctrl+1 zoom, Ctrl+2 dibujo, Ctrl+3 temporizador) y el canal local de comandos; Ctrl+C o SIGTERM lo detienen |
| `pixelpointer zoom` | Abre el zoom ahora; con el `daemon` en ejecución, le envía el comando |
| `pixelpointer draw` | Abre el dibujo sin zoom; misma regla que `zoom` |
| `pixelpointer timer` | Abre el temporizador de pausa (10 min) sobre la pantalla oscurecida; flechas o rueda ajustan los minutos y reinician, R/G/B/Y/O/P cambian el color, Esc o clic derecho salen; misma regla que `zoom` |
| `pixelpointer status` | Informa si el daemon está en ejecución, sin abrir una superposición |
| `pixelpointer quit` | Detiene el daemon y una superposición activa |
| `pixelpointer overlay [--image archivo.png] [--mode zoom\|draw\|timer]` | Abre la superposición sobre una captura nueva o sobre una imagen |
| `pixelpointer capture [--out archivo.png]` | Captura la pantalla e informa la latencia |
| `pixelpointer probe` | Diagnóstico: SO, sesión, monitores, versiones de los portales |
| `pixelpointer install-desktop-file` | Linux: instala el `.desktop` con el app id `io.github.lzocateli.PixelPointer` |
| `pixelpointer uninstall-desktop-file` | Linux: elimina recursos del usuario solo si no se modificaron; conserva los archivos cambiados |
| `pixelpointer grant` | GNOME/Wayland: pide el permiso de captura desde una ventana enfocada |
| `pixelpointer setup` | Primer uso opcional con diagnóstico, registro, solicitud de captura e inicio del daemon explícitos |
| `pixelpointer selftest` | Verifica las bibliotecas nativas sin pantalla (lo usan las pruebas de paquete) |
| `pixelpointer --version` / `--help` | Versión y ayuda |

Teclas en la superposición: rueda del mouse o flechas para el zoom, clic y
arrastrar para dibujar, Shift/Ctrl/Ctrl+Shift/Tab al empezar a arrastrar para
línea recta/rectángulo/flecha/elipse, R/G/B/Y/O/P para colores, Shift+color para
marcador translúcido, X/Shift+X para desenfoque/desenfoque fuerte, T para texto, Ctrl+rueda para
el grosor, Ctrl+Z para deshacer, E para borrar, W/K para pizarra blanca/negra,
Ctrl+S para guardar un PNG en la carpeta de capturas del usuario, Ctrl+C para
copiar la imagen al portapapeles, Esc o clic derecho para salir.

Ctrl+C copia la imagen visible, con anotaciones, como PNG. La copia sigue
disponible mientras el `daemon` esté en ejecución; en una superposición abierta
sin daemon, se pierde al salir de PixelPointer.

Las formas siguen a ZoomIt: mantén la tecla al empezar a arrastrar; la forma
sigue al mouse hasta soltar el botón y usa el color y el grosor actuales.
El desenfoque actúa sobre la captura y las anotaciones existentes; rectángulo y
elipse con desenfoque se rellenan, útiles para ocultar datos. El dibujo
transparente no tiene captura que desenfocar. Una tecla de color vuelve al lápiz
normal.

El texto empieza en la posición del puntero con el color actual; el tamaño sigue
el grosor (Ctrl+rueda antes de pulsar T). Mientras escribes, las teclas se
vuelven texto: Enter añade una línea, Backspace borra, Esc o un clic termina.
Ctrl+Z después de terminar elimina el texto completo.

## Dónde guarda Ctrl+S

| Sistema | Destino |
| --- | --- |
| Windows | Carpeta especial **Imágenes** del usuario + `Screenshots`, respetando redirecciones como OneDrive |
| macOS | Preferencia `location` de `com.apple.screencapture`; si no existe, el Escritorio del usuario |
| Linux (X11 y Wayland) | Carpeta `PICTURES` de XDG User Directories + `Screenshots` |

En Linux, la configuración se lee de `$XDG_CONFIG_HOME/user-dirs.dirs` o, si
`XDG_CONFIG_HOME` no está definido, de `~/.config/user-dirs.dirs`. Por ejemplo,
`XDG_PICTURES_DIR="$HOME/Imágenes"` se resuelve como `~/Imágenes/Screenshots`.
Si la carpeta de imágenes falta o está deshabilitada, configúrala en las
preferencias del escritorio o con la herramienta XDG de la distribución antes
de guardar. No existe un destino universal compartido por otras herramientas de
captura.

La configuración se consulta en cada Ctrl+S. La aplicación crea el destino si
es necesario, usa nombres únicos y nunca sobrescribe capturas existentes. En
Linux y macOS, los PNG nuevos solo permiten lectura y escritura al usuario
(0600). La ruta final o el error aparece en la superposición y, si está
disponible, en la terminal. Si falla, no guarda silenciosamente en el directorio
actual ni en otra carpeta.

El PNG contiene la escena y las anotaciones de la superposición, sin cursor ni
mensaje de guardado. En el dibujo transparente X11, el fondo del PNG también es
transparente: exporta las anotaciones sin volver a capturar el escritorio. Para
guardar la pantalla congelada con anotaciones, usa el zoom; W/K exportan
explícitamente la pizarra blanca/negra.

## Configuración

El archivo `settings.json` es opcional; sin él, se aplican los valores
predeterminados de abajo. La aplicación solo lee el archivo, en cada
superposición, y nunca lo crea ni lo reescribe. `pixelpointer probe` muestra la
ruta efectiva y si el archivo se cargó.

| Sistema | Ruta |
| --- | --- |
| Windows | `%APPDATA%\PixelPointer\settings.json` |
| macOS | `~/Library/Application Support/PixelPointer/settings.json` |
| Linux | `$XDG_CONFIG_HOME/pixelpointer/settings.json` o `~/.config/pixelpointer/settings.json` |

```json
{
  "version": 1,
  "pen": { "color": "red", "width": 6 },
  "zoom": { "initial": 2 }
}
```

| Campo | Valores | Predeterminado |
| --- | --- | --- |
| `version` | `1` | `1` |
| `pen.color` | `red`, `green`, `blue`, `yellow`, `orange`, `pink` | `red` |
| `pen.width` | 1 a 60 | 6 |
| `zoom.initial` | 1 a 16, ampliación al abrir el zoom | 2 |
| `rendering.gpu` | `true` o `false`; dibuja con OpenGL | `false` |

Los campos omitidos usan el valor predeterminado; se aceptan comentarios `//` y
comas finales. Un campo desconocido, un valor fuera de rango, JSON mal formado,
un archivo de más de 64 KiB o que no sea UTF-8 invalidan todo el archivo: la
superposición se abre con los valores predeterminados y el motivo aparece en la
terminal y en `probe`. Las teclas siguen cambiando color y grosor durante la
sesión sin escribir en el archivo.

`rendering.gpu` es experimental y está desactivado por defecto. Al activarlo, la
superposición intenta OpenGL y vuelve a la renderización en CPU, con un aviso en
la terminal, si no se pueden crear la ventana o el contexto OpenGL. El dibujo
transparente X11 y `SDL_RENDER_DRIVER=software` siguen en CPU. La línea
`quadros` de la terminal informa el camino usado.

## Permisos por sistema

### Linux con Wayland (GNOME, KDE, Hyprland)

Wayland no permite que un programa vea la pantalla o el teclado de otros;
PixelPointer usa los portales XDG del escritorio.
El dibujo en Wayland usa la captura autorizada por el portal y permanece
congelado; este backend no implementa transparencia sobre contenido en vivo.

1. `pixelpointer install-desktop-file`: sin el `.desktop`, el escritorio no
   asocia permisos ni atajos a PixelPointer. El paquete `.deb` ya lo instala.
2. `pixelpointer grant` (GNOME): abre una ventana de PixelPointer; pulsa Enter y
   haz clic en **Permitir**. GNOME solo muestra este diálogo a la aplicación
   enfocada, por eso no se puede pedir desde la terminal.
3. `pixelpointer daemon`: la primera vez, el escritorio muestra los atajos
   propuestos (Ctrl+1 y Ctrl+2) para que los confirmes o cambies. Después, el
   vínculo es inmediato.

Si el escritorio no ofrece el portal GlobalShortcuts (Sway, GNOME anterior a la
48), crea atajos del propio escritorio que ejecuten el comando:

| Escritorio | Configuración |
| --- | --- |
| GNOME | Configuración → Teclado → Atajos personalizados: comando `pixelpointer zoom`, atajo Ctrl+1 |
| KDE Plasma | Preferencias del sistema → Atajos → Añadir orden: `pixelpointer zoom` |
| Sway | `bindsym Ctrl+1 exec pixelpointer zoom` en `~/.config/sway/config` |
| Hyprland | `bind = CTRL, 1, exec, pixelpointer zoom` en `hyprland.conf` |

Con el `daemon` en ejecución, `pixelpointer zoom` solo envía el comando por el
socket local (`$XDG_RUNTIME_DIR/pixelpointer.sock`) y termina en milisegundos.

**Captura por ScreenCast.** Fuera de GNOME, con un solo monitor, cada Ctrl+1/Ctrl+2 abre una
sesión del portal ScreenCast, lee un cuadro por PipeWire (la `libpipewire-0.3`
del sistema) y cierra la sesión; el indicador de pantalla compartida aparece
solo durante esa captura. La primera vez el escritorio pide elegir y autorizar
el monitor. El *restore token* devuelto por el portal se guarda en
`$XDG_STATE_HOME/pixelpointer/screencast-restore-token` (o
`~/.local/state/...`) con permiso 0600 y evita repetir el diálogo; borra el
archivo para revocarlo. Cancelar el diálogo cancela la superposición. Con más
de un monitor, sin PipeWire o sin el portal ScreenCast, la captura usa el portal
Screenshot. En compositores wlroots (Sway, Hyprland), el cuadro solo llega tras
algún cambio en la pantalla; si no, la captura usa Screenshot tras 2 s. La
terminal informa el camino usado y el tiempo del portal y del primer cuadro.

En GNOME (`XDG_CURRENT_DESKTOP` con `GNOME`), la captura usa siempre el portal
Screenshot, autorizado una vez con `pixelpointer grant`: en GNOME 46 el
indicador de pantalla compartida quedaba fijo tras capturas ScreenCast cortas,
incluso sin sesión activa. La latencia de Screenshot es mayor.

Con varios monitores no se usa ScreenCast silenciosamente para capturar solo
uno: se selecciona Screenshot. Sus errores D-Bus se presentan como errores
controlados de captura, sin excepción no tratada. Si no hay ruta disponible
para ese escenario, la captura falla; no equivale a soporte multi-monitor
validado en todos los escritorios.

El nombre de la fuente corresponde a la última captura completada: tras
recuperarse ScreenCast de un fallback, vuelve a indicarse ScreenCast.
Un fallback fallido no informa una fuente como completada y la cancelación
no activa otra captura para eludir la negativa.

**Diagnóstico por etapas.** Se conservan los totales `portal` y `primeiro quadro`:
`portal` cubre creación de sesión, selección/inicio del portal y persistencia
de la autorización; `primeiro quadro` incluye abrir el remoto PipeWire y leer/
convertir el cuadro. El mensaje también informa las duraciones por etapa:

| Campo | Etapa |
| --- | --- |
| `sessao` | Crear la sesión ScreenCast |
| `fontes` | Seleccionar fuentes de captura |
| `inicio` | Iniciar sesión; incluye espera del consentimiento si el portal muestra diálogo |
| `persistencia` | Intentar guardar autorización para la próxima captura |
| `remoto` | Abrir la conexión PipeWire |
| `leitura` | Obtener y convertir el primer cuadro |
| `restore solicitado` | `sim`/`nao`: se proporcionó autorización anterior; nunca muestra su contenido |

Los valores en milisegundos se redondean por separado; su suma puede diferir
de los totales mostrados.

`restore solicitado sim` no demuestra que el portal reutilizó autorización
ni evitó el diálogo. Las mediciones excluyen consultas iniciales de capacidades,
lectura de autorización, cierre de sesión y presentación de la superposición;
no son la latencia completa de atajo a cuadro visible. No compares capturas con
y sin diálogo como escenarios equivalentes. El diagnóstico no cambia
consentimiento, backend, timeout o alternativa, ni expone tokens o contenido de pantalla.

**Consulta de capacidades.** La captura intenta leer `version`,
`AvailableSourceTypes` y `AvailableCursorModes` en una llamada `Properties.GetAll`.
Los valores no se guardan en cache entre capturas. Si `GetAll` falla, no está
soportado o devuelve propiedades ausentes/tipos inesperados, la conexión usa
las lecturas `Get` anteriores con su orden y salidas anticipadas; una conexión
nueva vuelve a intentar `GetAll`. La primera comprobación incompatible cuesta
una llamada adicional, pero no se repite en esa conexión. Permisos,
consentimiento, cursor, persistencia y alternativa de captura no cambian.

`ScreenCast capacidades: ... ms; consulta GetAll` indica lectura agrupada;
`consulta Get` indica compatibilidad mediante lecturas individuales. El tiempo
separado no está incluido en los totales anteriores ni es la latencia completa
del atajo. Menos consultas no demuestra por sí solo mejora perceptible ni la
meta de rendimiento de tu escritorio.

### Linux con X11

Los atajos y la captura funcionan sin permisos adicionales (XGrabKey y XGetImage).
En X11 no hace falta ejecutar `pixelpointer grant`. Si otra aplicación ya
registró Ctrl+1 o Ctrl+2, el daemon informa el conflicto y no anuncia ese atajo
como disponible. En el modo de dibujo, un compositor X11 detectado permite
anotaciones transparentes sobre el contenido actual. Sin compositor, la
aplicación avisa y usa una captura congelada; eso no es transparencia en vivo.

### Ejecución en segundo plano y salida

Esc o cerrar la superposición termina solo la sesión visual; el daemon sigue
activo. Usa `pixelpointer status` para consultar su estado y `pixelpointer quit`
para detenerlo, incluso si hay una superposición abierta. El menú de la bandeja
ofrece **Zoom**, **Desenhar** y **Sair** cuando SDL y el escritorio disponen de
ella. Ctrl+C o SIGTERM también detienen el daemon. Si la bandeja no está
disponible, usa `status` y `quit`; GNOME puede requerir una extensión de bandeja.

`Tray registrada` confirma el registro en el backend, no que el panel muestre el
icono. El daemon debe seguir ejecutándose después de ese mensaje. Al iniciarlo
desde una terminal, `pixelpointer daemon` se ejecuta en primer plano y ocupa esa
terminal; Ctrl+C lo detiene. En Linux, agrega `&` para liberar el indicador:

```bash
pixelpointer daemon &
```

El proceso aún puede escribir en esa terminal y puede detenerse al cerrar su
ventana. Para usarlo sin terminal, instala la entrada del escritorio y abre
**PixelPointer** desde el menú de aplicaciones. Esto no activa el inicio
automático.

### macOS

En la primera captura, macOS pide el permiso de **Grabación de pantalla** en
Configuración del Sistema → Privacidad y seguridad. La versión 1 no está firmada
con Developer ID, por lo que macOS puede volver a pedirlo tras cada
actualización. Los atajos globales nativos llegarán en una versión posterior;
mientras tanto, asocia `pixelpointer zoom` a un atajo del sistema
(Atajos/Automator) o a un lanzador como Raycast.

### Windows

No hay permisos adicionales. Ctrl+1 y Ctrl+2 se registran con `RegisterHotKey`;
si otro programa ya usa la combinación, el `daemon` lo informa.

## Verificar las descargas

```bash
sha256sum --check --ignore-missing SHA256SUMS
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer \
  --predicate-type https://spdx.dev/Document/v2.2
```

## Desinstalar

| Instalado con | Eliminación |
| --- | --- |
| `install.sh` | `rm -rf ~/.local/lib/pixelpointer ~/.local/bin/pixelpointer` (o el `--prefix` usado) |
| `install.ps1` | borra `%LOCALAPPDATA%\Programs\pixelpointer` |
| `.deb` | `sudo apt remove pixelpointer` |
| Homebrew | `brew uninstall pixelpointer` |
| winget | `winget uninstall lzocateli.PixelPointer` |

Si el daemon está en ejecución, detenlo primero con `pixelpointer quit`. Después,
desinstala PixelPointer con el mismo método que usaste para instalarlo:

| Instalado con | Comando o acción |
| --- | --- |
| `install.sh` (Linux/macOS, prefijo predeterminado) | En Linux, ejecuta `pixelpointer uninstall-desktop-file` si registraste la entrada del menú; luego ejecuta `rm -f -- "$HOME/.local/bin/pixelpointer"` y `rm -r -- "$HOME/.local/lib/pixelpointer"` |
| `install.sh --prefix <dir>` | Elimina `<dir>/bin/pixelpointer` y `<dir>/lib/pixelpointer`, sustituyendo `<dir>` por el prefijo usado durante la instalación |
| `install.sh --global` | Ejecuta `sudo rm -f -- /usr/local/bin/pixelpointer` y `sudo rm -r -- /usr/local/lib/pixelpointer`; en Linux, elimina primero la entrada del escritorio con el binario instalado |
| `install.ps1` | En PowerShell, ejecuta `Remove-Item -LiteralPath "$env:LOCALAPPDATA\Programs\pixelpointer" -Recurse` |
| `.deb` | `sudo apt remove pixelpointer` |
| Homebrew | `brew uninstall pixelpointer` |
| winget | `winget uninstall lzocateli.PixelPointer` |

Si agregaste manualmente la línea de inicio de PowerShell al perfil, elimínala
de `$PROFILE.CurrentUserAllHosts`. En Linux, elimina la integración del escritorio
antes del ejecutable con `pixelpointer uninstall-desktop-file`, usando el mismo
`XDG_DATA_HOME`; el comando conserva los archivos modificados. La desinstalación
no borra las capturas PNG. El paquete `.deb` administra por separado los recursos
instalados en `/usr/share`. En GNOME, los atajos vinculados se eliminan en
Configuración; el permiso de captura, con:

En Linux, elimina también `~/.local/share/applications/io.github.lzocateli.PixelPointer.desktop`
si usaste `install-desktop-file`. En GNOME, los atajos vinculados se eliminan en
Configuración; el permiso de captura, con:

```bash
gdbus call --session --dest org.freedesktop.impl.portal.PermissionStore \
  --object-path /org/freedesktop/impl/portal/PermissionStore \
  --method org.freedesktop.impl.portal.PermissionStore.DeletePermission \
  screenshot screenshot io.github.lzocateli.PixelPointer
```

## Solución de problemas

| Síntoma | Causa probable y solución |
| --- | --- |
| `erro: O portal recusou a solicitação (código 2)` | Falta el permiso de captura en GNOME: ejecuta `pixelpointer grant` |
| `aviso: registro do app id falhou` | Falta el `.desktop`: ejecuta `pixelpointer install-desktop-file` |
| Falló el registro del app id en Registry | Consulta `pixelpointer probe`; la ausencia de Registry se informa como limitación del portal, no como motivo para reinstalar la entrada del escritorio |
| Atajos no disponibles en Wayland | Escritorio sin el portal GlobalShortcuts: usa un atajo del escritorio (tabla anterior) |
| La ventana se abre pero no recibe el teclado | Compositor sin activation token; haz clic en la ventana una vez |
| Bandeja registrada, pero no aparece el icono en GNOME | Confirma que `pixelpointer status` responde y que el daemon no se haya abortado. La visibilidad depende del soporte StatusNotifier/AppIndicator del panel; usa la CLI si no está disponible. La aplicación no instala extensiones. |
| Ctrl+S informa un destino inválido o inaccesible | Revisa la configuración de la carpeta de capturas y sus permisos; la aplicación no guarda en otra ubicación alternativa |
| El daemon aborta tras registrar la bandeja con `_dbus_check_is_valid_utf8` | Usa un paquete reconstruido con la corrección de vida útil de textos de tray de SDL 3.5.0; el abort también libera los atajos |
| `selftest falhou` en Linux | glibc anterior a 2.34 o falta `libfontconfig`: usa Ubuntu 22.04, Debian 12, RHEL 9 o posteriores e instala `libfontconfig1` |
| Retraso de ~0,5 s al abrir en Wayland | Costo del portal Screenshot; la captura continua por PipeWire está prevista para la F2 |

Por ahora, los mensajes del programa están en portugués. Para informar un
problema, incluye la salida de `pixelpointer probe` y sigue el
[soporte](../../.github/SUPPORT.md).

### Recuperarse tras un abort del daemon

Un abort nativo puede dejar el socket en el disco aunque el proceso y los grabs
X11 hayan terminado. Por seguridad, el siguiente daemon rechaza esa ruta y no la
elimina automáticamente. Ejecuta `pixelpointer status` y revisa los procesos de
tu usuario con el monitor del sistema. Solo después de confirmar que no hay un
daemon activo y que la ruta pertenece a tu usuario, elimina **solo**
`$XDG_RUNTIME_DIR/pixelpointer.sock` (si `XDG_RUNTIME_DIR` está definido) o
`~/.cache/pixelpointer/run/pixelpointer.sock` (alternativa). No elimines
directorios ni sockets de otras aplicaciones. Reinicia con el paquete corregido.

## Limitaciones actuales

- El temporizador (Ctrl+3), Live Zoom (Ctrl+4), la grabación (Ctrl+5), el
  recorte (Ctrl+6) y DemoType (Ctrl+7) aún no existen; consulta el
  [plan](../plano-implementacao.md).
- Con varios monitores en Wayland, el compositor elige el monitor de la ventana.
- macOS sin atajos globales nativos y sin firma en la versión 1.

## Guion de aceptación manual en X11

Usa una sesión **X11 real**, sin `SDL_VIDEODRIVER=dummy`, con contenido de
prueba que no incluya datos personales. Se necesita el binario Native AOT
`linux-x64` compilado localmente (genera el RID correspondiente para otra
arquitectura). Desde la raíz del repositorio, compila y valida en contenedores:

```bash
pwsh ./tools/scripts/Invoke-Validation.ps1 -Aot -Engine podman
pwsh ./tools/scripts/Publish-Aot.ps1 -Rid linux-x64 -Engine podman
pwsh ./tools/scripts/Test-AotPackage.ps1 -Rid linux-x64 -Engine podman
```

Resultado esperado: compilación y pruebas aprobadas, paquete válido. Los comandos
generan artefactos locales; no instalan la aplicación en el host ni validan el
flujo gráfico. Docker puede sustituir Podman con `-Engine docker`.

En la sesión gráfica del usuario, desde la raíz del repositorio, usa el paquete
generado, sin `sudo`:

```bash
./artifacts/publish/linux-x64/pixelpointer probe
./artifacts/publish/linux-x64/pixelpointer grant
./artifacts/publish/linux-x64/pixelpointer daemon
```

1. **Diagnóstico:** `probe` debe identificar X11 e informar si hay compositor.
  `grant` debe explicar que la captura nativa X11 no necesita permiso del
  portal. La ausencia de Registry no debe recomendar reinstalar la entrada del
  escritorio; los errores reales de registro deben seguir distinguiéndose.
2. **Atajos y captura:** pulsa Ctrl+1, anota sobre una imagen conocida, amplía y
  cierra con Esc. El daemon debe seguir activo. Compara con
  `./artifacts/publish/linux-x64/pixelpointer zoom` en otra terminal. Repite
  Ctrl+1 y Ctrl+2 con las cuatro combinaciones de CapsLock/NumLock.
3. **Dibujo en vivo:** con compositor, muestra un reloj o una animación y pulsa
  Ctrl+2. El contenido debe seguir moviéndose bajo los trazos, sin fondo opaco.
  W/K solo deben cambiar las pizarras seleccionadas explícitamente. Sin
  compositor, espera un aviso y una captura congelada, no transparencia.
4. **PNG:** pulsa Ctrl+S dos veces y comprueba el mensaje, dos archivos distintos
  y el destino XDG. En el zoom, el PNG debe incluir la captura y los trazos; en
  el dibujo transparente debe conservar el alfa. Si el destino no permite
  escritura, espera un error en la superposición y ningún archivo en otra
  carpeta. Usa una cuenta sin privilegios para esta prueba.
5. **Bandeja y salida:** abre y cierra Zoom/Desenhar desde la bandeja. Elige
  **Sair** con y sin una superposición abierta. Si la bandeja no está disponible,
  confirma el aviso y ejecuta `./artifacts/publish/linux-x64/pixelpointer quit`
  en otra terminal. Consulta `status` antes y después; tras salir debe devolver
  1 y no debe quedar ningún daemon.
6. **Instancia única y reinicio:** con el daemon activo, ejecuta `daemon` desde
  otra terminal. Debe fallar sin afectar la primera instancia. Detén y reinicia
  la primera; los atajos deben volver sin grabs residuales.
7. **Menú sin terminal:** detén el daemon, ejecuta `install-desktop-file` con el
  mismo binario y abre **PixelPointer** desde el menú de aplicaciones. Comprueba
  el icono, el aviso de Ctrl+S y la salida por bandeja/CLI, sin terminal. Si hay
  conflicto con una entrada existente, consérvala y revisa el mensaje; no borres
  archivos para forzar la instalación. Para deshacer solo esta integración,
  ejecuta `uninstall-desktop-file` con el mismo binario.
8. **Conflictos y monitores:** en una sesión de prueba, reserva Ctrl+1 para otra
  aplicación antes de iniciar el daemon; confirma el conflicto y que Ctrl+2 siga
  funcionando si está libre. Repite la captura, la alineación de los trazos y el
  guardado en monitores con distintas escalas/resoluciones y posiciones negativas.

Registra SO, arquitectura, escritorio/compositor, versión de PixelPointer,
escala, monitores y resultado de cada paso. No publiques capturas sensibles,
tokens ni registros completos. Compositor, bandeja, atajos físicos y varios
monitores requieren aceptación manual; SDL dummy no los valida.
