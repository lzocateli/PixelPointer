# Plan de E2E gráficos aislados con uv y evidencia en vídeo

[Português (Brasil)](../plano-e2e-grafico-uv.md) · [English](../en/plano-e2e-grafico-uv.md) · **Español**

| Campo | Estado |
| --- | --- |
| Fecha | 2026-10-30 |
| Autorización humana | Solicitante, en esta conversación: autorización de E2E gráficos aislados según las reglas del proyecto |
| Decisión autorizada | Permitir una suite gráfica automatizada separada y aislada |
| Estado de ejecución | X11 ejecutado con estabilidad 10/10; revisión humana de vídeos cerrada por decisión del mantenedor el 2026-10-31, sin revisión registrada. ScreenCast/PipeWire validado en Sway aislado con render node y modo sin GPU tratado. Captura, zoom y dibujo de v0.1.3-preview.14 aceptados manualmente en Ubuntu 26.04.1/GNOME Wayland el 2026-10-31; token, indicador, latencia sin diálogo y matriz adicional pendientes |
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
| Wayland | ScreenCast/PipeWire pasó en Sway aislado; indisponibilidad sin GPU tratada. v0.1.3-preview.14 aceptada manualmente para captura, zoom y dibujo en Ubuntu 26.04.1/GNOME Wayland, con consentimiento observado. Reutilización del token, duración del indicador, latencia sin diálogo, varios monitores y otros compositores pendientes; sin mocks de consentimiento ni XTEST vía XWayland como prueba nativa |
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

### Aceptación manual de preview en GNOME Wayland

El 2026-10-31, el solicitante informó que el test Linux Wayland pasó,
adjuntó una captura y el log, y autorizó explícitamente registrar zoom y
dibujo como correctos. La prueba se ejecutó en su máquina, no mediante
automatización conectada a su sesión gráfica.

- Entorno observado: Ubuntu 26.04.1 LTS x64, GNOME/Wayland, Native AOT,
  `0.1.3-preview.14` del commit `cdd125e`; `probe` informó ScreenCast v5 y
  GlobalShortcuts v1. Versión de GNOME y escala no fueron proporcionadas
  explícitamente.
- La captura muestra el diálogo con el monitor interno seleccionado y la
  instalación de la entrada de escritorio y el icono. El aviso de registro
  del app en `probe` fue anterior a esa instalación; no hay diagnóstico nuevo
  que confirme su resolución.
- El log confirma **xdg-desktop-portal ScreenCast + PipeWire**, PNG de
  **1920×1080**, portal **20.830 ms**, primer cuadro **122 ms** y total
  **20.999 ms**. El tiempo del portal incluye elección y confirmación humana
  en el diálogo; no demuestra la meta de atajo a cuadro < 150 ms.
- Captura, zoom y dibujo tienen **aceptación manual en este entorno** según
  confirmación humana. La imagen estática no demuestra por sí sola la
  interacción de zoom/dibujo; su aceptación se basa en el relato.
- Esta preview sigue sin evidencia de reutilización del token, duración del
  indicador, repetición sin consentimiento, atajos físicos, acciones de
  bandeja/lanzador, Ctrl+S, alfa o contenido en vivo, varios monitores,
  otros compositores, escalas, GPU y Windows/macOS. La confirmación no
  completa la matriz Wayland automatizada ni las fases F2/F3.

El adjunto permanece en la conversación, sin copia al repositorio; no se
publica la captura personal. La ruta personal del PNG se omitió del registro.

### Validación del diagnóstico por etapas el 2026-10-31

El binario Native AOT local con el diagnóstico aún no publicado pasó la
prueba ScreenCast/PipeWire en Sway aislado con render node. El runner existente
verificó dos capturas, colores/formato y cuadro nuevo tras cambios de escena.
nginx permaneció aislado sin puertos públicos, con smoke y limpieza; no se
accedió a sesión gráfica ni D-Bus del host.

Evidencias locales: `artifacts/wayland/screencast-20261101T015920989-305aa712`.
Primera captura: portal 9 ms, remoto 3 ms, lectura 19 ms, campo anterior
`primeiro quadro` 23 ms, `restore solicitado nao`. Segunda: portal 8 ms,
remoto 4 ms, lectura 16 ms, campo anterior 20 ms, `restore solicitado sim`.
Valores redondeados por separado; describen esta ejecución, no un benchmark
equivalente a GNOME con diálogo ni prueba de la meta < 150 ms.

El gate local pasó format, build sin avisos/errores, 139 pruebas unitarias,
23 casos de gobernanza de preview, publicación Native AOT y `selftest`.
La clase ScreenCast tuvo 23 pruebas aprobadas tras preparar Fontconfig en
el container. No se repitieron pruebas gráficas nativas Windows/macOS ni
aceptación manual GNOME del diagnóstico nuevo. No completa F2/F3.

### Validación de consulta agrupada el 2026-10-31

El binario Native AOT local, aún no publicado, pasó de nuevo en Sway aislado
con render node, nginx aislado sin puertos públicos y limpieza de containers.
Evidencias: `artifacts/wayland/screencast-20261101T022349873-b4ed0c2d`.
Ambas capturas, colores/formato y cuadro nuevo tras cambios de escena pasaron;
ambas informaron `ScreenCast capacidades: 0 ms; consulta GetAll`, confirmando
la ruta agrupada. El valor redondeado no implica costo cero ni es un benchmark
en GNOME.

El gate pasó format, build sin avisos/errores, 156 pruebas unitarias,
23 casos de gobernanza de preview, Native AOT y `selftest`. Las 24 pruebas
del cliente de portales cubren tipos/propiedades, monitor y secuencia anterior
con salidas anticipadas y cancelación. Respuestas incompatibles y lecturas
anteriores se probaron como lógica sintética, no contra un portal real que
rechace `GetAll`. La matriz GNOME/KDE/otros portales y el benchmark completo
siguen pendientes; esta ejecución no completa F2/F3.

### Compatibilidad GetAll bajo rechazo controlado

El 2026-11-01 pasaron el escenario normal y el de rechazo de `GetAll` con el
mismo paquete Native AOT local y la imagen Wayland existente. nginx estuvo
aislado sin puertos públicos, con smoke y limpieza. No se accedió a sesión
gráfica, D-Bus ni directorios personales del host; solo se expuso al container
rootless el render node ya autorizado.

- Control: `artifacts/wayland/screencast-20261101T121050665-764bf12c`,
  dos capturas ScreenCast/PipeWire con cuadro actualizado, sin política adicional.
- Rechazo: `artifacts/wayland/screencast-20261101T121052886-6dcf124c`,
  `result.json` con `passed=true`, `mode=getall-denied` y dos rechazos.
  Ambas capturas mantuvieron ScreenCast/PipeWire e informaron `consulta Get`.
- Configuración D-Bus derivada de la imagen por parser XML con DTD y resolución
  externa desactivadas, creada sin sobrescritura y montada como solo lectura.
  La política bloquea `Properties.GetAll` de `org.freedesktop.portal.Desktop`
  en el container, sin examinar argumentos. `Get`, creación/inicio de sesión
  y lectura de cuadros siguen permitidos.
- El log del bus comprueba los rechazos. El test exige ambos diagnósticos `Get`
  y capturas correctas; un fallo posterior escribe `passed=false`, sin conservar
  el resultado positivo del runner. No se sustituyó el portal ni su consentimiento.

Comando adicional desde la raíz del clone, con paquete actual en
`artifacts/aot/linux-x64`, Podman rootless, imagen Wayland disponible y nginx
aislado ya iniciado según las reglas E2E:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -DenyScreenCastGetAll
```

Exige el diagnóstico nuevo de capacidades, no el paquete de preview anterior.
Sin `RenderNode`, se rechaza antes de iniciar el escenario; ayuda y argumentos
desconocidos se probaron en container. No cambió código de producción en esta
etapa; también se repitió y aprobó el escenario normal. Cada comando `capture`
abre una conexión nueva: no comprueba capturas repetidas con preferencia `Get`
en una sola conexión del daemon. Portales naturalmente incompatibles, GNOME/KDE,
matriz multiplataforma y meta de latencia siguen pendientes. La inyección no
concluye el plan.

### Capturas repetidas en la conexión del daemon

El 2026-11-01 pasó `-RepeatDaemonCaptures` en control y con
`-DenyScreenCastGetAll`, usando el mismo paquete AOT local. El driver Python
usa solo biblioteca estándar y herramientas presentes en la imagen; ejecuta
el baseline existente y luego un daemon real en D-Bus/Sway descartable.
Un monitor registra solo llamadas de propiedades, sin respuestas, tokens
ni contenido de pantalla. Las capturas inspeccionan una escena sintética.
nginx estuvo aislado sin puertos públicos, con smoke y limpieza.

| Escenario | Evidencias locales | Resultado |
| --- | --- | --- |
| Control | `artifacts/wayland/screencast-20261101T123222089-3df3f7ae` | Un daemon, dos sesiones, dos `GetAll`, ningún rechazo |
| Rechazo | `artifacts/wayland/screencast-20261101T123225959-89b40b94` | Un daemon, una `GetAll` rechazada, seis `Get` ordenados para versión/fuentes/cursor en dos sesiones |

El driver identifica la conexión D-Bus por PID del daemon. Dibujo y zoom se
activan por IPC; se exige una ventana y una captura por portal verifica el
color, diferente en la segunda sesión. `swaymsg` cierra la ventana; el proceso
debe seguir activo según `status` y `quit` terminar con código cero.
`daemon-result.json` registra llamadas, colores y ciclo de vida; el orquestador
exige resultado completo y propaga fallos a `result.json`. Inicialización
o verificación incompleta no significa éxito.

Desde la raíz del clone, con imagen, paquete actual, render node autorizado,
Podman rootless y nginx aislado iniciado según las reglas anteriores:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -RepeatDaemonCaptures
```

Añade `-DenyScreenCastGetAll` para repetir bajo rechazo. El driver se monta
solo lectura; exige render node y no cambia el escenario normal. El orquestador
sigue en PowerShell 7 y Python solo se ejecuta dentro del container. No cambió
producción. Este alcance aislado comprueba reutilización de conexión y consultas
nuevas, no cambios de propiedades en un portal naturalmente incompatible,
atajos físicos, Esc, bandeja ni quit con ventana abierta. GNOME/KDE y meta
de latencia siguen pendientes.

### Quit con superposición abierta y reinicio

El 2026-11-01, la rama `test/wayland-daemon-lifecycle` añadió
`-CheckDaemonLifecycle` como opción, exigiendo `-RepeatDaemonCaptures`.
No cambió código de producción ni dependencias en esta etapa.

El driver exige superposición abierta y verifica su color mediante el portal
antes de `quit`. El proceso debe terminar con código cero, desaparecer la
ventana y `status` indicar ausencia del daemon. Luego inicia otro proceso,
exige identidad D-Bus nueva, abre zoom por IPC, verifica cuadro actualizado
y termina con `quit`. El JSON registra las aserciones; el orquestador rechaza
resultados incompletos del ciclo de vida.

| Escenario | Evidencia local | Resultado |
| --- | --- | --- |
| Prueba anterior sin opción nueva | `artifacts/wayland/screencast-20261101T130137628-161f4830` | Aprobada; comportamiento anterior preservado |
| Ciclo de vida normal | `artifacts/wayland/screencast-20261101T130141551-ca85f33c` | Quit con ventana abierta, status parado, conexión nueva y captura tras reinicio aprobados |
| Ciclo de vida con rechazo `GetAll` | `artifacts/wayland/screencast-20261101T130145567-e269262d` | Mismas verificaciones aprobadas bajo política de rechazo |

Desde raíz del clone, con paquete AOT actual, Podman rootless, render node
autorizado y nginx aislado según los requisitos anteriores:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -RepeatDaemonCaptures -CheckDaemonLifecycle
```

Añade `-DenyScreenCastGetAll` para repetir bajo rechazo. nginx permaneció sin
puertos públicos y pasó smoke; se eliminaron containers. No se accedió a sesión
gráfica, D-Bus ni datos personales del host. Se usó el AOT validado de etapa 1.
Es cobertura Sway aislado/IPC, no atajos físicos, Esc, bandeja, lanzador ni
aceptación nativa GNOME/Windows/macOS.

### Política multi-monitor y errores de Screenshot

El 2026-11-01, `test/wayland-multi-monitor-policy` validó la política existente:
ScreenCast solo con exactamente un monitor. Cinco casos unitarios cubren
conteos negativos, cero, uno, dos y tres. Errores D-Bus del fallback Screenshot
se convierten en errores controlados, conservando cancelación y el mensaje
de indisponibilidad del fallback anterior.

`-CheckMultiMonitorPolicy` ejecuta el baseline y crea dos salidas virtuales
Sway activas, verificadas por JSON. La imagen no ofrece Screenshot; `capture`
debe terminar con código 1, sin PNG, mensaje ScreenCast ni excepción no tratada.
Comprueba política e indisponibilidad segura, no captura multi-monitor exitosa.

| Escenario | Evidencia local | Resultado |
| --- | --- | --- |
| Sin render node | `artifacts/wayland/screencast-20261101T131754378-903d3b19` | Indisponibilidad y mensaje anterior preservados |
| Un monitor y ciclo de vida | `artifacts/wayland/screencast-20261101T131758088-7c112c2c` | Captura, quit y reinicio siguen aprobados |
| Dos salidas sin Screenshot | `artifacts/wayland/screencast-20261101T131802565-e28d49cf` | `passed=true`, 2 salidas, código 1, ningún archivo, error controlado |

Desde raíz del clone, con AOT actual, imagen existente, Podman rootless,
render node autorizado y nginx aislado según los requisitos anteriores:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -CheckMultiMonitorPolicy
```

No combina con `-RepeatDaemonCaptures`. El gate pasó format, build sin
avisos/errores, 161 pruebas, 23 casos de gobernanza, AOT y `selftest`.
nginx no publicó puertos y se eliminaron containers. No se usó sesión gráfica,
D-Bus ni captura personal del host. Captura multi-monitor real con Screenshot,
selección de monitor, HiDPI, GNOME/KDE y otras arquitecturas/plataformas pendientes.

### Identidad de fuente tras fallback y recuperación

El 2026-11-01, `fix/capture-backend-recovery` separó captura nativa y selección
con fallback mediante `IScreenCapture`. El nombre cambia solo tras éxito:
vuelve a ScreenCast después de recuperarse y no cambia a Screenshot si falla
el fallback. Cancelación sigue sin fallback; sesión, consentimiento, timeout
y cierre nativos no cambiaron.

Tres pruebas focadas usan fuentes controladas para éxito → fallback → recuperación,
fallback fallido y cancelación. El gate pasó format, build sin avisos/errores,
164 pruebas, 23 casos de gobernanza, AOT y `selftest`.

| Escenario nativo | Evidencia local | Resultado |
| --- | --- | --- |
| Sin render node | `artifacts/wayland/screencast-20261101T174558524-1a07a163` | Error controlado, ningún archivo |
| Captura y ciclo de vida | `artifacts/wayland/screencast-20261101T174601492-fccadfc0` | Draw/zoom por IPC, status, quit y reinicio aprobados |
| Rechazo `GetAll` | `artifacts/wayland/screencast-20261101T174606129-0bb81304` | Lecturas Get y capturas reales preservadas |

nginx aislado sin puertos públicos; no se accedió a sesión ni D-Bus del host.
La recuperación de identidad se probó como lógica controlada, no con portal
real cambiando disponibilidad. Captura real probada en Sway; GNOME/KDE y
cobertura de hardware pendientes.

### Investigación de escala fraccionaria bloqueada

Registro histórico del bloqueo inicial; la validación posterior figura en la sección siguiente.

El 2026-11-01, `test/wayland-output-scale` intentó pruebas con escalas 1, 1,5 y 2.
La de 1,5 no pasó: la salida lógica Sway era `533×400`, pero la ventana siguió
`1×1`, sin fullscreen. Evidencia:
`artifacts/wayland/screencast-20261101T134318812-3740613a/window-geometry.json`.
El resultado aislado no invalida la aceptación humana anterior en GNOME.

Un probe SDL descartable comparó tamaño inicial cero, tamaño explícito y
fullscreen solicitado tras crear la ventana. Candidatos locales pasaron build,
161 pruebas, AOT/selftest y E2E X11, pero no la matriz Wayland: ventana pequeña
en la segunda sesión y fallo de captura de inspección de contenido estático.
No se clasificaron como correcciones aprobadas.

Se retiraron los experimentos de producción y daño de composición;
`OverlaySession.cs` vuelve a coincidir con `main`. AOT experimental conservado en
`artifacts/investigation/fullscreen-experimental-536b8551f28b4f36af044186905856f3`;
no usarlo como implementación validada. Reconstruir desde fuente antes de
nuevas pruebas. Evidencias de fallo y pruebas de escala permanecen locales,
sin commit/push/PR. Incremento **bloqueado**, no concluido. Las escalas 1,5 y 2
no recibieron aceptación de esta prueba; geometría, composición e inspección
de cuadros en el fixture requieren investigación antes de entregar.

### HiDPI aislado tras separar la inspección

El 2026-11-01 se separaron dos problemas: tamaño inicial cero/fullscreen no
aplicado en Wayland e inspección de superposición estática mediante una segunda
sesión ScreenCast que puede quedar sin daño en wlroots. La corrección usa tamaño
lógico del display al crear en Wayland y solicita fullscreen tras crear el
renderer solo si no se aplicó la flag. No bloquea con `SDL_SyncWindow` ni cambia
X11/Windows/macOS, consentimiento, timeout o GPU opcional.

`localhost/pixelpointer-wayland:screencast-v2` fija `grim=1.4.0+ds-2+b1` del
snapshot Debian existente. Licencia Expat/MIT de la versión exacta, incluyendo
`debian/*`, verificada extrayendo copyright del `.deb` firmado sin ejecutar
scripts; paquete suministrado por Debian trixie mantenida. Sitios upstream/
metadatos web bloquearon acceso automatizado; versión obtenida del APT del
snapshot, no presumida. Links nativos: wayland-client/libffi y pixman (MIT),
libpng, zlib y runtime de la distribución, presentes en la imagen; inventario
en `/opt/native-packages.tsv`. Solo pruebas, no paquetes o captura del producto.

El baseline sigue exigiendo ScreenCast/PipeWire real. El driver inspecciona
pixels de la escena sintética mediante grim en el compositor, sin otra sesión
de portal; no comprueba consentimiento, cubierto por el baseline. Exige
fullscreen y límites lógicos mediante JSON Sway, tamaño físico 800×600, color,
densidad y ciclo de vida. No se reclasificaron los fallos anteriores.

| Escala | Evidencia local | Resultado |
| --- | --- | --- |
| 1 | `artifacts/wayland/screencast-20261101T143030541-59f79fcb` | Dibujo, zoom, status, quit y reinicio aprobados |
| 1,5 | `artifacts/wayland/screencast-20261101T143034841-b65dc44a` | Lógica 533×400, pixels 800×600, densidad 1,5; sesiones y ciclo de vida aprobados |
| 2 | `artifacts/wayland/screencast-20261101T143038653-a3d322d0` | Lógica 400×300, pixels 800×600, densidad 2; sesiones y ciclo de vida aprobados |

Format, build sin avisos/errores, 161 pruebas, 23 casos de gobernanza, AOT y
`selftest` aprobados. E2E X11 aprobado en
`artifacts/e2e/x11-20261101T143148779-500ce3d3/attempt-01`. nginx aislado sin
puertos públicos; no se usaron sockets ni datos gráficos personales del host.

Con imagen v2 construida, AOT actual, render node autorizado y nginx aislado
según requisitos anteriores:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Build -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -RepeatDaemonCaptures -CheckDaemonLifecycle -OutputScale 1.5
```

`OutputScale` acepta 1/1.5/2; distinto de 1 exige `RepeatDaemonCaptures`.
Capturas del compositor no comprueban atajos físicos, lápiz/presión, paridad
GPU, multi-monitor ni aceptación nativa GNOME/KDE/Windows/macOS. Esos recortes
y benchmark de latencia siguen pendientes; F2/F3 no concluidas.

### Primer uso opcional y cierre sin efectos

El 2026-11-01, `feat/first-use-setup` añadió `setup` explícito. La política
habilita registro Linux, solicitud de captura Wayland e inicio del daemon solo
sin instancia activa. Fechar es la selección inicial; no cambiaron comandos
existentes ni inicio del daemon.

14 pruebas cubren política y límites de clic. El gate pasó format, build sin
avisos/errores, 178 pruebas, 23 casos de gobernanza, AOT y `selftest`.
`-CheckSetupWindow` verificó región de ventana no vacía y cierre por compositor
sin modificar archivos del app ni estado del daemon, con y sin daemon activo.
Evidencia: `artifacts/wayland/screencast-20261101T182402112-1a823db2`.
nginx aislado sin puertos públicos; no se usaron datos gráficos personales del host.

No se solicitó captura ni concedieron permisos automáticamente. Consentimiento
GNOME real, controles físicos y flujo nativo Windows/macOS pendientes.
“Solicitação finalizada” no comprueba permiso concedido; los detalles del
diagnóstico existente quedan en la terminal.

Generar **preview nueva tras integrar este PR en main con CI verde**, antes del
aceite manual de primer uso. No esperar toda la F2; la preview actual no tiene
`setup`. Tras generar y aprobar el Environment `release`, probar GNOME/Wayland
y Windows: abrir/cerrar sin acciones, diagnóstico, registro explícito cuando
corresponda, cancelación/consentimiento voluntario e inicio elegido del daemon.
No quitar Pre-release ni promover estable antes de validar el alcance afectado.

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

### Validación ScreenCast + PipeWire en Wayland aislado

`tools/scripts/Test-WaylandScreenCast.ps1` ejecuta el paquete en un contenedor con
sway headless, PipeWire, WirePlumber, xdg-desktop-portal y
xdg-desktop-portal-wlr (snapshot Debian fijado), sin red y sin la sesión, D-Bus ni
directorios del host. El selector de xdg-desktop-portal-wlr está configurado para
la única salida virtual; no hay diálogo de GNOME en este entorno.

| Modo | Verificación | Resultado el 2026-10-31 |
| --- | --- | --- |
| Predeterminado, sin GPU | xdg-desktop-portal-wlr 0.7 exige DMA-BUF y rechaza el renderer pixman ([#289](https://github.com/emersion/xdg-desktop-portal-wlr/issues/289)); `capture` debe fallar con código 1, aviso de retorno a Screenshot y error claro, sin excepción no controlada ni archivo | Aprobado |
| `-RenderNode /dev/dri/renderD128` (Podman rootless, `keep-id`) | Dos capturas 800x600 por ScreenCast, color exacto del fondo, restore token 0600 reutilizado y cuadro nuevo tras cambiar el fondo | Aprobado: portal 7–8 ms, primer cuadro 19–26 ms, 36 ms en total |

El modo con GPU expone solo el nodo de renderizado, de forma opcional. Los tiempos
son del entorno virtual sin diálogo; no sustituyen la medición en GNOME real,
donde la meta sigue siendo < 150 ms. En wlroots el cuadro solo llega tras daño en
la pantalla; sin daño, la captura vuelve a Screenshot tras 2 s.

## 9. Referencias técnicas

- [Scripts y lockfiles uv](https://docs.astral.sh/uv/guides/scripts/).
- [Servidor virtual Xvfb](https://www.x.org/releases/current/doc/man/man1/Xvfb.1.xhtml).
- [Entrada emulada libei/EIS](https://libinput.pages.freedesktop.org/libei/).
- [Recorrido manual F0, pt-BR](../f0-relatorio.md#6-roteiro-de-teste-manual-por-plataforma).
- [Recorrido manual X11](uso.md#guion-de-aceptaci%C3%B3n-manual-en-x11).
