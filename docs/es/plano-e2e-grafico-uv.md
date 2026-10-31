# Plan de E2E gráficos aislados con uv y evidencia en vídeo

[Português (Brasil)](../plano-e2e-grafico-uv.md) · [English](../en/plano-e2e-grafico-uv.md) · **Español**

| Campo | Estado |
| --- | --- |
| Fecha | 2026-10-30 |
| Autorización humana | Solicitante, en esta conversación: autorización de E2E gráficos aislados según las reglas del proyecto |
| Decisión autorizada | Permitir una suite gráfica automatizada separada y aislada |
| Estado de ejecución | Primer incremento X11 implementado y ejecutado; estabilidad 10/10; revisión humana de vídeos cerrada por decisión del mantenedor el 2026-10-31, sin revisión registrada. Aceptación manual adicional Ubuntu 26.04/GNOME Wayland registrada abajo; no es E2E automatizado |
| Versión funcional de referencia | v0.1.2, primera versión funcional aceptada como estable por el mantenedor el 2026-10-30 en entornos GNOME X11/Wayland registrados; no es aprobación de la matriz multiplataforma completa |
| Primer objetivo | Linux x64, X11 en escritorio desechable |
| Expansión | GNOME X11, Wayland por compositor y otros sistemas tras demostrar viabilidad |

## 1. Objetivo y límites

Ejercitar el paquete Native AOT como usuario: iniciar, activar atajos globales,
dibujar, guardar, interactuar con la bandeja y salir. Generar resultados verificables,
vídeo y datos suficientes para repetir ejecuciones y diagnosticar regresiones.

El [README](../../README.es.md) define el producto; la [guía de uso](uso.md)
define comandos y comportamientos. Las [reglas de pruebas, pt-BR](../../.github/instructions/testing.instructions.md)
registran la autorización. Este plan no sustituye esos contratos ni el
[plan de la issue #3, pt-BR](../plano-correcao-issue-3.md).

No implementar funcionalidades futuras solo para probarlas. Temporizador, Live
Zoom, grabación del propio producto, recorte y DemoType quedan fuera de la cobertura
inicial. La grabación de este plan pertenece al entorno de pruebas, no a PixelPointer.

## 2. Hipótesis y validaciones que pueden refutarlas

| Hipótesis | Cómo refutar |
| --- | --- |
| Entrada X11 simulada ejercita la ruta global real | Con otra ventana enfocada, enviar Ctrl+1/Ctrl+2 mediante servidor X y exigir apertura; IPC no sustituye esta prueba |
| Entorno virtual admite dibujo transparente | Animar el fondo, mantener un trazo encima y comprobar cambios del fondo conservando el trazo; imágenes estáticas o SDL `dummy` no lo demuestran |
| Bandeja funciona desde la perspectiva del usuario | Localizar icono y pulsar físicamente entradas; consulta/evento D-Bus aislado solo demuestra protocolo |
| Vídeo documenta toda la interacción | Grabar antes de la primera acción, comprobar cuadros decodificables, duración y etapas finales |
| Suite identifica regresiones | Provocar fallo controlado, como detener daemon, y exigir fallo con evidencias conservadas |
| Ejecución es repetible | Repetir desde entorno limpio, mismas versiones/referencias, sin intervención manual |

## 3. Entorno y aislamiento

Arquitectura implementada en el primer incremento:

```text
Docker/Podman en host: solo orquestación
├── Escritorio de prueba en contenedor
│   ├── Servidor X11 virtual
│   ├── Gestor de ventanas, compositor y panel StatusNotifier
│   ├── D-Bus privado y configuración desechable
│   ├── Paquete Native AOT y escena visual sintética
│   ├── Runner Python ejecutado con uv
│   └── Captura de pantalla y codificación de vídeo
└── nginx en contenedor: borde exclusivo de cualquier servicio HTTP
```

- No montar sockets X11/Wayland, D-Bus, directorios personales, secretos ni
  dispositivos de entrada del host. No usar `--privileged`, red del host ni
  inyección global en el host.
- Usuario sin privilegios, red privada, límites de recursos y plazo global.
  Paquete de solo lectura; escritura limitada al directorio de ejecución.
- Aislar `HOME`, configuración XDG, runtime, capturas y preferencias por escenario.
  Cleanup no puede borrar recursos de otra ejecución, incluso tras fallo.
- Xvfb es candidato a servidor X11, no escritorio completo. Confirmar composición/
  renderizado necesarios antes de aprobar imagen.
- Mantener nginx al ejecutar la aplicación. Visualización remota/auxiliares HTTP
  solo por ese borde, sin puertos directos. Automatización nativa y D-Bus usan
  canales privados, no HTTP.
- No exigir HTTP, navegador ni Playwright para controlar SDL. La visualización
  remota es opcional y no condiciona la primera entrega.

## 4. Runner, dependencias y reproducibilidad

Proyecto Python en `tests/e2e/`, con preparación y colección de evidencias compartidas.
El [runbook, pt-BR](../../tests/e2e/README.md) registra comandos, versiones, licencias,
amenazas y recuperación. Python, manifiesto y lockfile fijados; build mediante
`uv sync --locked`, runtime sin conexión mediante `uv run --locked --no-sync`,
sin actualización automática de dependencias.

Antes de elegir cada componente:

1. Verificar licencia permisiva de versión exacta y dependencias transitivas,
   mantenimiento y compatibilidad de distribución MIT.
2. Fijar uv, Python, herramientas nativas e imagen por digest.
3. Registrar fuentes, licencias y versiones; bloquear licencias no verificadas.

No asumir que cualquier distribución FFmpeg, codec o biblioteca de automatización
está permitida. Elegir grabador/codec tras comprobarlo. uv resuelve dependencias
Python, no fija por sí solo servidor gráfico, fuentes ni codecs.

En X11 preferir eventos XTEST mediante servidor gráfico. Llamadas internas/eventos
SDL inyectados no sustituyen atajos globales. Coordenadas relativas a ventanas/
regiones conocidas, geometría controlada. Fijar resolución, escala, tema, fuentes,
locale, zona horaria y renderizado. Escena determinista con formas, colores y región
animada, sin datos personales. Esperar condiciones con plazos; evitar secuencias
basadas únicamente en `sleep`.

## 5. Matriz de escenarios actuales

| Grupo | Acciones | Aserciones obligatorias |
| --- | --- | --- |
| Daemon e IPC | Iniciar, status, intentar segunda instancia | Disponibilidad, duplicación rechazada, primer proceso conservado |
| Atajos globales | Ctrl+1/Ctrl+2 con foco externo, alternar CapsLock/NumLock | Ventana/modo esperados; daemon conservado |
| Conflicto | Otro cliente registra combinación antes del daemon | Error explícito; no anunciar grab no disponible como éxito |
| Zoom | Rueda, flechas, movimiento del ratón | Escala/región cambian según acción |
| Anotaciones | Arrastrar; R/G/B/Y/O/P; Ctrl+rueda | Geometría, colores y grosores dentro de tolerancias |
| Edición | Ctrl+Z y E | Eliminación del último trazo/todos los trazos, respectivamente |
| Pizarras | W y K | Fondo blanco/negro y anotaciones según contrato |
| Transparencia | Dibujar sobre escena animada con compositor | Fondo sigue cambiando; trazos persisten; PNG contiene alfa |
| Sin compositor | Dibujar sin compositor | Aviso explícito y captura congelada; sin afirmar transparencia |
| Guardado | Ctrl+S repetido, destino XDG con espacios y acentos | PNG decodificable, carpeta correcta, nombres únicos, archivos conservados |
| Configuración | `settings.json` con color, grosor y zoom inicial; después un archivo inválido | `probe` carga el archivo; zoom 4x, trazo azul grueso; el archivo inválido genera un aviso, usa los valores predeterminados y queda intacto |
| Fallo de guardado | Configuración inválida o destino inaccesible | Error visible, sin destino alternativo, proceso utilizable |
| Salir de superposición | Esc y botón derecho donde corresponda | Transiciones del contrato, reapertura posible, daemon conservado |
| Bandeja gráfica | Pulsar icono y Zoom/Desenhar/Sair | Icono visible, menú/acción correctos, cierre limpio |
| Bandeja no disponible | Iniciar sin host StatusNotifier | Limitación explícita; status/quit funcionales |
| Ciclo de vida | Quit con superposición, reiniciar, iniciar por menú | Recursos liberados, reinicio funcional, inicio sin terminal |
| CLI y escritorio | Help, version, probe, capture, instalación/eliminación por usuario | Salida/archivo coherentes, identidad propia, conflictos conservados |

Reutilizar pruebas de filesystem, IPC, paquete y PNG sin duplicarlas íntegramente.
E2E añade interacción observable, incluidos mensajes en pantalla. Grant/permisos de
portal pertenecen a expansión Wayland, no aceptación X11. Presión de lápiz, escala
fraccionaria y multimonitor requieren escenarios propios; ratón y monitor virtual
único no los validan.

## 6. Evidencias y criterios de aceptación

Cada ejecución necesita id y directorio de artefactos exclusivos, sin sobrescribir
resultados previos: vídeo por escenario capturado del escritorio (no montado desde
PNG del producto), capturas antes/después y PNG exportados, informe legible y JUnit
XML, línea de tiempo estructurada de acciones/tiempo monotónico/verificaciones,
manifiesto de hash de paquete/digest de imagen/versiones/parámetros gráficos y
diagnóstico sanitizado limitado a la prueba, sin logs ni entorno completos.

Grabar antes de actuar y comprobar disponibilidad. Detener correctamente incluso
en fallo, conservando vídeo parcial recuperable. Fallo de grabación obligatoria
reprueba ejecución; no aceptar vídeo vacío ni éxito sin evidencia. Sin audio por defecto.

Fijar y versionar estos criterios del primer incremento:

- Código cero solo si pasan todas las aserciones/evidencias obligatorias.
- Timeout, crash o funcionalidad obligatoria omitida no pueden producir éxito.
- Verificaciones visuales con regiones, tolerancias y referencias explícitas,
  sin actualización automática de referencias.
- Análisis PNG de dimensión, color/alfa y contenido, no solo existencia.
- Vídeo decodificable cubriendo acciones, tolerancia de duración según frame rate.
- Diez ejecuciones limpias consecutivas sin fallos intermitentes.
- Fallo controlado detectado, informe reprobado y evidencia útil conservada.

Reintentos no deben ocultar el primer fallo. Registrar cada intento; no reclasificar
inestabilidad como aprobación estable. Definir presupuesto temporal y retención de
vídeos en implementación; conservar fallos hasta análisis y no publicar vídeos automáticamente.

## 7. Etapas y entregables

1. **Inventariar contratos y componentes.** Verificar licencias, versiones,
   backend de entrada, captura y compositor/panel antes de añadir dependencias.
2. **Construir entorno mínimo.** Escritorio desechable, escena y runner uv;
   build/smoke de imagen, aislamiento y limpieza en error.
3. **Prueba vertical.** Daemon → Ctrl+1 → dibujo → Ctrl+S → Esc → icono/menú →
   Sair. Vídeo, aserciones y ejecución limpia repetible.
4. **Demostrar detección de regresión.** Forzar fallo en subproceso; comprobar
   código no cero, vídeo/informe. Si es viable, usar paquete anterior defectuoso
   sin modificar/revertir worktree actual.
5. **Expandir X11.** Matriz de sección 5, con/sin compositor y bandeja, conflictos,
   modificadores y errores de guardado.
6. **Integrar ejecución y evidencias.** Comando único desde raíz, requisitos,
   resultados y recuperación; host solo orquesta contenedores.
7. **Expandir compatibilidad.** GNOME X11 real aislado, Wayland por compositor,
   monitores/escala y arquitecturas adicionales tras comprobar soporte.

Comandos, preparación de imagen, ejecución con lockfile e inspección de resultados
en el [runbook, pt-BR](../../tests/e2e/README.md). No editar `gw-*` ni `tools/scripts/ci/`:
pertenecen al catálogo Groundwork. Integración CI en superficies propias autorizadas
solo tras prueba local en contenedores.

## 8. Compatibilidad, riesgos y aceptación pendiente

| Entorno | Plan y límite |
| --- | --- |
| Linux x64, X11 virtual mínimo | Ejecutado: 10/10 consecutivos con compositor/bandeja; variantes sin compositor y sin bandeja pasaron |
| GNOME X11 | Aceptación manual de dibujo transparente, visual automático, zoom, PNG, CapsLock/NumLock, ambos monitores y cierre por Sair; capturas confirman monitor 1, icono/menú y status sin daemon; Zoom/Desenhar por bandeja, lanzador y matriz completa sin demostración individual |
| Wayland | Aceptación manual Ubuntu 26.04/GNOME tras instalar v0.1.2: flujos ejercitados funcionales según relato, capturas de dibujo, pizarras, lanzador y bandeja. E2E aislado por compositor, libei/EIS y portales reales pendientes; sin mocks de consentimiento ni XTEST vía XWayland como prueba nativa |
| Linux arm64 | Ejecutar en arquitectura adecuada antes de declarar cobertura |
| Windows/macOS | Backends nativos y sesiones gráficas desechables; estrategia depende de compatibilidad con contenedores y autorización específica para excepciones |
| Lápiz/GPU/monitores físicos | Uso en ambos monitores 1920×1080 escala 1 confirmado manualmente; lápiz, otras GPU, escalas/topologías pendientes |

Riesgos: diferencias de renderizado/foco/temporización, panel sin SNI, compositor
virtual limitado, vídeo incompleto y dependencias incompatibles. Separar fallos de
infraestructura/producto; ambos bloquean escenarios obligatorios. No desactivar
transparencia para aprobar una prueba que promete validarla.

Aceptación final exige matriz ejecutada con límites, evidencias reproducibles y
revisión humana de vídeos/referencias iniciales. Autorización permite construir
suite, no aprueba herramientas, excepciones de seguridad, soporte multiplataforma
ni resultados todavía inexistentes.

### Resultado local del 2026-10-30

Etapas 1–6 implementadas para primer objetivo; etapa 7 es expansión condicionada,
no cobertura declarada. Podman rootless, sin acceso al escritorio real del
solicitante ni cambios en workflows gestionados.

Imagen final: `16ddca00a969716ee1db935ea42c0a9200a82051b49a9718345ca0ad6ca513cb`.
SHA-256 ejecutable: `eae12d3d269670a7da1914ea028fb7cc478f9aa8e0416b1849134b2338f1eb31`.
Manifiestos también registran hashes de bibliotecas y archivos del paquete.

| Verificación | Resultado y directorio local en `artifacts/e2e/` |
| --- | --- |
| Estabilidad, diez escritorios limpios | 10/10; `x11-20261030T175407362-09a7e896/attempt-01` a `attempt-10` |
| Muerte deliberada del daemon | Código 1, `Daemon interrompido: -9`, JUnit fallido, vídeo conservado; `x11-20261030T175704158-6db4df5b/attempt-01` |
| Sin compositor | Pasó con aviso y PNG opaco; `x11-20261030T175751758-897e6ec5/attempt-01` |
| Sin host StatusNotifier | Pasó con limitación explícita/control conservado; `x11-20261030T175842166-cb50e0dc/attempt-01` |
| Integridad de evidencias | 13 directorios, 750 cuadros JPEG decodificados; resolución, conteo, JUnit, JSON y acción final comprobados |
| Colector | 3 pruebas pasaron: truncamiento, vídeo vacío, conteos y conservación de fallos |
| Calidad .NET | Formato/build sin avisos/errores; 59 pruebas, Native AOT y selftest pasaron |

E2E encontró un defecto: píxeles BGRX de XGetImage podían tener cero en el byte no
usado, conservado como alfa cero por SKImage. Zoom negro/transparente pese a captura
directa correcta. La copia ahora normaliza ese byte a 255. Prueba Skia reprodujo
fallo antes de corrección y pasó después, igual que zoom/dibujo transparente en E2E.

Con compositor, visual TrueColor de 32 bits seleccionado explícitamente mediante
hint público SDL, sin GLX. Panel/lanzador sintéticos reciben clics XTEST reales;
no demuestra integración GNOME ni visual automático. Sin servidor remoto ni
Playwright: producto SDL nativo; nginx aislado con smoke HTTP y sin puertos públicos.
Revisión humana de vídeos/referencias y otros entornos pendientes.

### Aceptación manual del dibujo transparente en GNOME X11

El 2026-10-30 el solicitante ejecutó paquete en su sesión, confirmó éxito y aportó
PNG de dibujo. Prueba realizada por usuario; automatización no accedió a su sesión.

- Entorno de `probe`: Ubuntu 24.04.5 LTS x64, GNOME/X11, compositor presente,
  dos monitores 1920×1080 y escala 1.
- Inicio sin `SDL_VIDEO_X11_WINDOW_VISUALID`, `SDL_VIDEODRIVER` ni `SDL_RENDER_DRIVER`;
  SDL seleccionó X11 y renderer OpenGL automáticamente.
- Confirmación humana de dibujo transparente, seguida de prueba alfa solicitada.
  Logs registraron zoom con captura 3840×1080 y dos guardados en ese modo.
- Aceptación humana explícita complementaria del 2026-10-30 de CapsLock/NumLock
  y otros monitores; pruebas concluidas en configuración informada.
- Tres capturas complementarias del monitor 1: icono propio en panel, menú
  Zoom/Desenhar/Sair, anotaciones rojas con identificación manuscrita del monitor.
  Visibilidad del icono/apertura del menú verificadas. Adjuntos en conversación,
  no repositorio. Fondo mostrado no demuestra ni refuta alfa del PNG.
- Solicitante confirmó cierre por Sair y captura con regreso al prompt y `status`
  indicando `Daemon não está em execução`. Acción según relato; prompt/status como
  evidencia visual. Flujo bandeja → Sair aceptado en GNOME/X11; sin prueba de código
  de salida, inspección de recursos, variantes con/sin superposición ni inicio por lanzador.
- PNG analizado aparte en contenedor: RGBA, 1920×1080; 2.054.243 píxeles alfa cero,
  13.881 alfa 255 y 5.476 intermedios. Demuestra fondo transparente y contenido
  opaco/parcial del archivo; comportamiento en vivo es relato humano, no inferencia.
- SHA-256: `564c0d4658dafc50d081ecd63c35bd482e1433b4b5d4da8b65595926aa72344d`.
  Archivo personal fuera del repositorio, sin copia/publicación; solo el archivo
  designado explícitamente se montó en lectura para análisis.

Resuelve visual automático en ese entorno, confirma exportación transparente,
CapsLock/NumLock, ambos monitores, monitor 1 e icono/menú GNOME. Sair tiene aceptación
humana y status sin daemon. No demuestra activación Zoom/Desenhar por bandeja,
lanzador, conflictos físicos, escala fraccionaria, otras GPU/topologías, Wayland
ni matriz GNOME completa; no sustituye revisión de vídeos automatizados.

### Aceptación manual en Ubuntu 26.04/GNOME Wayland

El 2026-10-30, tras la instalación de `v0.1.2` indicada en esta conversación,
el solicitante informó que las pruebas en Ubuntu 26.04/Wayland funcionaban y
adjuntó capturas. Es aceptación humana de flujos ejercitados, no ejecución
automatizada ni confirmación individual de toda la matriz.

| Alcance | Evidencia y resultado |
| --- | --- |
| Entorno | Ubuntu 26.04, GNOME/Wayland según relato/contexto; paquete de referencia linux-x64 v0.1.2. Capturas sin `--version`, `probe`, escala ni topología |
| Dibujo/colores | Trazos rojos/azules sobre imagen del escritorio; captura posterior con trazo azul nuevo |
| Pizarras/regreso | Fondos blanco/negro conservan trazos; otra captura regresa a imagen del escritorio con anotaciones |
| Identidad desktop | Icono/nombre PixelPointer en lanzador; icono del panel y menú Zoom/Desenhar/Sair visibles |
| Salida visual de anotación | Capturas sin trazos muestran escritorio; no prueban cierre del daemon ni comando usado |
| Resultado humano | Funcionamiento general confirmado en sesión; zoom, atajos, permisos y acciones del menú no demostrados individualmente |

Seis adjuntos quedan como evidencia de conversación, sin copia, hash, publicación
ni inspección de archivos personales. Automatización no accedió al escritorio.
Instalador publicado v0.1.2 comprobado aparte en conversación: exige/copia
`pixelpointer.svg`; capturas corroboran identidad visual, no sustituyen pruebas
de paquete/instalador.

**Aceptación complementaria del dibujo transparente del 2026-10-30:** el
solicitante aportó otras dos capturas en esta conversación, identificándolas
expresamente como evidencia de dibujo transparente. La primera muestra un trazo
rojo sobre la imagen de un vídeo en el navegador; la segunda, una anotación
circular roja sobre otro contenido del vídeo. El contenido del navegador sigue
visible bajo el área de dibujo. El dibujo transparente queda concluido por
aceptación humana en esta sesión Ubuntu 26.04/GNOME Wayland, corroborada
visualmente por la superposición. El registro tiene ocho adjuntos en total,
todos conservados solo en la conversación. No se analizó PNG original ni se
grabó/automatizó la sesión. Como anotaciones y contenidos difieren, las capturas
no prueban movimiento continuo del fondo bajo el mismo trazo persistente;
esa comprobación objetiva y el análisis de alfa del PNG exportado siguen pendientes.

Límites: imágenes estáticas no prueban transparencia en vivo, alfa PNG, latencia,
Wayland nativo frente a XWayland, ScreenCast/PipeWire ni portal utilizado. Inicio
sin terminal, conflictos, CapsLock/NumLock en este Wayland, fallos/rechazo de permisos,
otros monitores, HiDPI, lápiz, otros compositores y Windows/macOS no detallados en
este retest. Pendientes E2E Wayland aislado por compositor y revisión humana de
vídeos X11. Aceptación no completa toda F2/F3.

## 9. Referencias técnicas

- [Scripts y lockfiles uv](https://docs.astral.sh/uv/guides/scripts/).
- [Servidor virtual Xvfb](https://www.x.org/releases/current/doc/man/man1/Xvfb.1.xhtml).
- [Entrada emulada libei/EIS](https://libinput.pages.freedesktop.org/libei/).
- [Recorrido manual F0, pt-BR](../f0-relatorio.md#6-roteiro-de-teste-manual-por-plataforma).
- [Recorrido manual X11](uso.md#guion-de-aceptaci%C3%B3n-manual-en-x11).