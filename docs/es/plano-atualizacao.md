# Plan de actualización de PixelPointer

[Português (Brasil)](../plano-atualizacao.md) · [English](../en/plano-atualizacao.md) · **Español**

| Campo | Estado |
| --- | --- |
| Estado del plan | **Implementado** el 2026-11-04; pendiente de revisión humana y validación en Windows y macOS |
| Estado de la función | Implementada: `pixelpointer update` y el submenú **Atualizações** de la bandeja; reemplazo validado de extremo a extremo solo en Linux x64 |
| Seguimiento oficial | [IMPL-06 en la sección 8 del plan de implementación](plano-implementacao.md#8-pendientes-consolidados) |
| Fecha | 2026-11-04 |
| Origen | Propuesta del solicitante, detallada con asistencia de IA |
| Decisiones técnicas | Elegidas por el solicitante el 2026-11-04 y registradas en el [ADR 2026-0001](adr/2026-0001-atualizacao-opt-in.md) (`proposed`) |

## 1. Objetivo y estado actual

Definir actualizaciones optativas para instalaciones autogestionadas de
PixelPointer, sin interrumpir una presentación ni eludir los permisos del
sistema operativo. El [README](../../README.es.md#instalación) y la
[guía de releases](release.md#canales-de-distribución) describen el
comportamiento actual: se pueden volver a ejecutar los instaladores propios,
mientras que `.deb`, Homebrew y winget usan sus respectivos flujos de
actualización. Hasta esta implementación, la aplicación no buscaba ni instalaba
actualizaciones; el comportamiento implementado está en la
[guía de uso](uso.md#actualizar).

Este documento propone requisitos y una secuencia de trabajo. El estado de
ejecución se registra únicamente en IMPL-06 del
[plan principal](plano-implementacao.md#8-pendientes-consolidados).

## 2. Comportamiento propuesto

Preferencias visibles para el usuario:

| Opción | Valor inicial | Comportamiento |
| --- | --- | --- |
| `Buscar actualizaciones automáticamente` | Desactivada | Busca periódicamente una versión compatible e informa al usuario. La búsqueda manual sigue disponible. |
| `Instalar actualizaciones automáticamente` | Desactivada | Si está activada junto con la búsqueda automática, descarga, valida y prepara la actualización sin pedir confirmación adicional. No autoriza interrumpir una sesión activa. |

La instalación automática depende de la búsqueda automática: al desactivar la
primera opción también se suspende la instalación automática. La preferencia de
instalación debe quedar deshabilitada o desactivarse mientras la búsqueda esté
apagada.

Reglas según el origen de la instalación:

- Las instalaciones autogestionadas por usuario mediante `install.sh` o
  `install.ps1` pueden actualizarse automáticamente si el usuario actual puede
  escribir en el directorio y se identifica el origen de forma fiable.
- `.deb`, Homebrew y winget pueden buscar e informar de una nueva versión, pero
  no deben instalarla automáticamente. El aviso debe indicar el flujo del
  gestor correspondiente, descrito en la [guía de releases](release.md#canales-de-distribución).
- Las instalaciones globales, directorios que exijan elevación, orígenes
  desconocidos e instalaciones portátiles no registradas no deben ser
  reemplazadas por el actualizador. No solicitar ni eludir elevación.
- El canal estable es el predeterminado. Las versiones preview solo se buscan
  tras elegir explícitamente ese canal; no mezclar versiones preview y estables.

## 3. Flujo de actualización automática

1. Consultar metadatos de releases por HTTPS, respetando canal, plataforma,
   arquitectura y una frecuencia limitada. La búsqueda manual no respeta el
   intervalo. La consulta no debe enviar identificadores personales ni
   telemetría.
2. Si hay una versión compatible, mostrar las versiones instalada y disponible.
   Con la instalación automática activada, obtener el paquete correspondiente
   al origen autogestionado; no descargar ni ejecutar scripts de instalación
   remotos.
3. Descargar a un área temporal restringida y validar versión, RID, tamaño,
   integridad y procedencia antes de modificar la instalación. El mecanismo de
   autenticación de procedencia más allá de `SHA256SUMS` debe decidirse antes de
   implementar: una suma de comprobación no autenticada, por sí sola, no prueba
   el origen.
4. Preparar los archivos en un directorio de staging del mismo volumen que la
   instalación, ejecutar `--version` y `selftest` en la versión nueva, y dejar
   intacta la versión actual si falla cualquier paso.
5. Aplicar el reemplazo solo después de que el daemon y las ventanas hayan
   terminado normalmente, sin superposición, grabación u otra sesión activa.
   Si se necesita un proceso auxiliar que espere al cierre, debe ser específico
   de la plataforma, estar limitado al directorio autorizado y no tener
   privilegios elevados.
6. Realizar el reemplazo con recuperación de la versión anterior si falla la
   instalación. No reiniciar la aplicación automáticamente; el próximo inicio
   utilizará la versión actualizada y podrá informar del resultado.

Si una actualización ya se descargó pero todavía no se puede aplicar, informar
que se completará al salir de PixelPointer. Si falla la validación, el reemplazo
o la recuperación, conservar los archivos existentes y mostrar instrucciones
accionables; nunca eliminar la versión anterior para liberar espacio antes de
confirmar el éxito.

## 4. Etapas propuestas

1. **Cerrar requisitos y decisiones:** elegir intervalo, fuente de metadatos,
   experiencia de preferencias, política de canales y validación de procedencia.
2. **Identificar instalación y preferencias:** definir metadatos seguros para
   distinguir instalaciones por usuario, mediante gestor de paquetes, globales
   y desconocidas; guardar preferencias por usuario sin almacenar secretos.
3. **Implementar la búsqueda:** descubrimiento de versiones compatibles,
   búsqueda manual y automática, avisos y supresión de consultas repetidas.
4. **Implementar instalación en staging:** descarga, validación, selftest,
   aplicación diferida, recuperación y mensajes de estado para Windows, Linux y
   macOS, respetando los canales que permiten actualizarse directamente.
5. **Integrar y documentar:** actualizar la interfaz disponible en el producto,
   los instaladores, la documentación de instalación/releases y el soporte por
   canal.

## 5. Criterios de finalización

- Ambas preferencias se guardan por usuario, empiezan desactivadas y respetan
  la dependencia entre búsqueda e instalación automáticas.
- La búsqueda manual funciona aunque la búsqueda automática esté desactivada;
  se ignoran versiones incompatibles con el canal, SO o arquitectura.
- Las actualizaciones inválidas, interrumpidas, no confiables o sin permiso no
  modifican la instalación actual ni ejecutan contenido no validado.
- La actualización automática no interrumpe una superposición, grabación o
  daemon activo, no solicita elevación y conserva la versión anterior si falla.
- El actualizador nunca reemplaza `.deb`, Homebrew ni winget; se informa al
  usuario del flujo correspondiente al canal detectado.
- Las pruebas cubren políticas de preferencias, identificación de origen,
  integridad, staging, aplazamiento, fallos/rollback y avisos, además de las
  combinaciones de SO y arquitectura admitidas. Las pruebas que ejecuten la
  aplicación siguen las reglas de contenedores y nginx del repositorio.

## 6. Decisiones

Las decisiones siguientes fueron elegidas por el solicitante el 2026-11-04 y se
detallan en el [ADR 2026-0001](adr/2026-0001-atualizacao-opt-in.md), todavía
`proposed` hasta una revisión humana registrada.

- **Periodicidad:** solo en el daemon; 5 minutos después de iniciar y luego cada
  24 horas (1 hora después de un fallo).
- **Metadatos:** `releases/latest` solo descubre la tag estable; la identidad sale
  de los assets de la tag, de `SHA256SUMS`, de la atestación y del `--version` del
  binario atestado.
- **Procedencia:** verificación nativa del bundle Sigstore de la atestación de
  GitHub, sin `gh` ni dependencias nuevas.
- **Origen:** recibo `install-receipt.json` escrito por los instaladores, combinado
  con comprobaciones de escritura, permisos y privilegios del proceso.
- **Interfaz:** CLI `pixelpointer update` y submenú **Atualizações** de la
  bandeja; preferencias en `update.json`.
- **Aplicación:** al cerrar el daemon por decisión del usuario y con
  `pixelpointer update apply` con el daemon detenido; sin reinicio automático.
- **Canal preview:** no se ofrece en esta versión; solo se consultan releases estables.

## 7. Fuera de alcance

- Actualización silenciosa de `.deb`, Homebrew, winget o instalaciones que
  requieran elevación.
- Reinicio forzado, downgrade automático, migración destructiva de datos o
  ejecución de scripts obtenidos de la red.
- Telemetría, recopilación de identificadores personales y actualizaciones
  automáticas de dependencias de compilación.
