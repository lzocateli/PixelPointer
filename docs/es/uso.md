# Guía de uso

[Português (Brasil)](../uso.md) · [English](../en/usage.md) · **Español**

> Traducción de la referencia en portugués [docs/uso.md](../uso.md). Si
> difieren, prevalecen la versión en portugués y el comportamiento implementado.

## Comandos

| Comando | Función |
| --- | --- |
| `pixelpointer daemon` | Se mantiene en ejecución, registra los atajos globales (Ctrl+1 zoom, Ctrl+2 dibujo) y el canal local de comandos; Ctrl+C o SIGTERM lo detienen |
| `pixelpointer zoom` | Abre el zoom ahora; con el `daemon` en ejecución, le envía el comando |
| `pixelpointer draw` | Abre el dibujo sin zoom; misma regla que `zoom` |
| `pixelpointer overlay [--image archivo.png] [--mode zoom\|draw]` | Abre la superposición sobre una captura nueva o sobre una imagen |
| `pixelpointer capture [--out archivo.png]` | Captura la pantalla e informa la latencia |
| `pixelpointer probe` | Diagnóstico: SO, sesión, monitores, versiones de los portales |
| `pixelpointer install-desktop-file` | Linux: instala el `.desktop` con el app id `io.github.lzocateli.PixelPointer` |
| `pixelpointer grant` | GNOME/Wayland: pide el permiso de captura desde una ventana enfocada |
| `pixelpointer selftest` | Verifica las bibliotecas nativas sin pantalla (lo usan las pruebas de paquete) |
| `pixelpointer --version` / `--help` | Versión y ayuda |

Teclas en la superposición: rueda del mouse o flechas para el zoom, clic y
arrastrar para dibujar, R/G/B/Y/O/P para colores, Ctrl+rueda para el grosor,
Ctrl+Z para deshacer, E para borrar, W/K para pizarra blanca/negra, Ctrl+S para
guardar un PNG en el directorio actual, Esc o clic derecho para salir.

## Permisos por sistema

### Linux con Wayland (GNOME, KDE, Hyprland)

Wayland no permite que un programa vea la pantalla o el teclado de otros;
PixelPointer usa los portales XDG del escritorio.

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

### Linux con X11

Los atajos y la captura funcionan sin permisos adicionales (XGrabKey y XGetImage).

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
| Atajos no disponibles en Wayland | Escritorio sin el portal GlobalShortcuts: usa un atajo del escritorio (tabla anterior) |
| La ventana se abre pero no recibe el teclado | Compositor sin activation token; haz clic en la ventana una vez |
| `selftest falhou` en Linux | glibc anterior a 2.34 o falta `libfontconfig`: usa Ubuntu 22.04, Debian 12, RHEL 9 o posteriores e instala `libfontconfig1` |
| Retraso de ~0,5 s al abrir en Wayland | Costo del portal Screenshot; la captura continua por PipeWire está prevista para la F2 |

Por ahora, los mensajes del programa están en portugués. Para informar un
problema, incluye la salida de `pixelpointer probe` y sigue el
[soporte](../../.github/SUPPORT.md).

## Limitaciones actuales

- El temporizador (Ctrl+3), Live Zoom (Ctrl+4), la grabación (Ctrl+5), el
  recorte (Ctrl+6) y DemoType (Ctrl+7) aún no existen; consulta el
  [plan](../plano-implementacao.md).
- Con varios monitores en Wayland, el compositor elige el monitor de la ventana.
- macOS sin atajos globales nativos y sin firma en la versión 1.
