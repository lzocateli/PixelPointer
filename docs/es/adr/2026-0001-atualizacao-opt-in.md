# ADR 2026-0001: Actualización opt-in con procedencia Sigstore verificada por la aplicación

[Português (Brasil)](../../adr/2026-0001-atualizacao-opt-in.md) · [English](../../en/adr/2026-0001-atualizacao-opt-in.md) · **Español**

**Estado:** proposed

**Fecha:** 2026-11-04  
**Responsables:** Lincoln Zocateli  
**Origen:** asistido por IA; opciones elegidas por el solicitante en la sesión del 2026-11-04  
**Revisor humano:** pendiente  
**Relacionado:** [plan de actualización](../plano-atualizacao.md), IMPL-06 del [plan de implementación](../plano-implementacao.md#8-pendientes-consolidados)

## Contexto

El [plan de actualización](../plano-atualizacao.md) dejó seis decisiones abiertas:
periodicidad, fuente de metadatos, prueba de procedencia en tiempo de ejecución
sin `gh`, identificación del origen de la instalación, interfaz y momento de
aplicación. Las releases ya publican `SHA256SUMS`, SBOM y atestaciones Sigstore
keyless; los instaladores propios solo verifican el SHA-256. La aplicación es
Native AOT, sin reflexión, y `settings.json` es de solo lectura para ella.

## Fuerzas de decisión

- no instalar contenido sin prueba de origen ni reemplazar canales de terceros;
- no pedir ni eludir elevación y no interrumpir presentaciones;
- ninguna dependencia nueva, compatibilidad con AOT y ningún secret nuevo en CI;
- operación reversible que falla cerrada.

## Opciones consideradas

### Procedencia

- **Verificación nativa del bundle Sigstore (elegida):** reutiliza las atestaciones
  existentes, sin clave ni secret nuevos; exige incorporar el material de
  confianza público y seguir sus rotaciones.
- **Clave ECDSA propia firmando `SHA256SUMS`:** verificación simple, pero agrega
  un secret, rotación y un runbook nuevo.
- **Postergar la instalación:** sin riesgo de instalación, pero no cumple el objetivo.

### Origen de la instalación

- **Recibo de los instaladores y comprobaciones del sistema de archivos (elegida):**
  el recibo por sí solo no autoriza nada; se combina con escritura del usuario
  actual, ausencia de escritura de grupo u otros, proceso no elevado y exclusión
  de rutas de `.deb`, Homebrew y winget.
- **Solo heurística de rutas:** no distingue instalaciones portátiles.

## Decisión

1. **Periodicidad:** solo en el daemon, con la verificación automática activada:
   5 minutos después de iniciar y luego cada 24 horas; 1 hora después de un fallo.
   La verificación manual ignora el intervalo.
2. **Metadatos:** `GET /repos/lzocateli/PixelPointer/releases/latest` solo sirve
   para descubrir la tag estable. El paquete se obtiene de
   `releases/download/v<versión>/` y solo se acepta con el tamaño publicado, el
   SHA-256 de `SHA256SUMS` y la atestación; la versión también se confirma
   ejecutando `--version` en el binario atestado, lo que impide entregar otro
   binario legítimo como la versión anunciada.
3. **Procedencia:** verificación en C# con la BCL del bundle obtenido en
   `GET /repos/.../attestations/sha256:<digest>`: cadena Fulcio, SCT del log CT,
   SET y prueba de inclusión Merkle con checkpoint firmado de Rekor v1, firma
   DSSE, cuerpo del log vinculado al sobre y al certificado, statement in-toto
   SLSA v1 que cubre nombre y digest del paquete, e identidad: emisor
   `token.actions.githubusercontent.com`, repositorio
   `https://github.com/lzocateli/PixelPointer` con IDs numéricos `1439215418`
   (repositorio) y `16601706` (dueño), runner `github-hosted`, firmante
   `.github/workflows/gw-github-assets.yml` en `refs/heads/main` o `refs/tags/v*`.
4. **Origen:** `install.sh` e `install.ps1` escriben `install-receipt.json`
   (`installer`, `scope` user/global, `rid`). Solo el alcance de usuario que pasa
   las comprobaciones anteriores puede reemplazarse; los demás canales solo
   avisan con el comando del gestor.
5. **Interfaz:** CLI `pixelpointer update` y submenú **Atualizações**
   (Actualizaciones) en la bandeja; preferencias en `update.json`, escrito por la
   aplicación con reemplazo atómico, que se niega a sobrescribir un archivo
   inválido o modificado en paralelo.
6. **Aplicación:** al cerrar el daemon por decisión del usuario (bandeja, `quit`,
   Ctrl+C) y con `pixelpointer update apply`/`install` con el daemon detenido.
   Linux y macOS usan intercambio atómico de directorios (`renameat2`/`renamex_np`,
   con alternativa de dos renombrados y rollback); en Windows, la versión
   preparada completa el reemplazo cuando el proceso anterior termina. Sin
   reinicio automático.

Revisar esta decisión si GitHub migra las atestaciones a Rekor v2 o a otro
emisor, si cambia el workflow de release o si se pide un canal preview.

## Consecuencias

### Positivas

- Las instalaciones de usuario se actualizan sin `gh`, sin elevación y sin scripts remotos.
- Ninguna dependencia, clave ni secret nuevos.

### Negativas y compromisos

- El material de confianza de Sigstore y la identidad del workflow quedan
  incorporados: una rotación, Rekor v2 (Ed25519, sin soporte en la BCL) o un
  cambio del workflow hacen que la verificación falle cerrada hasta actualizar
  con el instalador.
- La API anónima de GitHub tiene límite por IP.
- Las instalaciones anteriores al recibo deben volver a ejecutar el instalador una vez.

## Seguridad y privacidad

Nada se ejecuta antes de aceptar el SHA-256 y la procedencia; solo se extraen los
archivos regulares esperados; el staging queda en el mismo volumen, en un
directorio privado; los procesos auxiliares nunca se elevan y se limitan al
directorio derivado de su propio ejecutable y del marcador. Sin telemetría: solo
un User-Agent fijo.

## Datos, protocolo y compatibilidad

Archivos nuevos: `install-receipt.json` (versión 1), `update.json` (versión 1) y
`<padre>/.pixelpointer.update/staged.json`. Los instaladores siguen siendo
compatibles con las instalaciones existentes.

## Observabilidad y operación

Eventos `UPDATE` en el log de diagnóstico (sin contenido capturado), estado en la
bandeja y en `pixelpointer update status`, y resultado de la última aplicación en
el siguiente inicio del daemon.

## Validación

- Pruebas unitarias con el bundle real de v0.3.1 y casos adulterados (payload, SET,
  prueba, checkpoint, log, identidad, digest).
- Pruebas con sistema de archivos real para preferencias, origen, extracción,
  staging, aplicación, rollback y limpieza.
- E2E en contenedor Ubuntu 24.04 con un binario 0.3.0 actualizado desde GitHub real
  a la release publicada; instalación global rechazada.
- Pendientes: Windows (auxiliar de reemplazo, `install.ps1`) y macOS (`renamex_np`).

## Despliegue y reversión

Opt-in y desactivado de forma predeterminada. Para apagarlo:
`pixelpointer update auto-check off`. Para revertir la función, una release sin el
comando mantiene las instalaciones funcionando; una actualización fallida
restaura la versión anterior automáticamente.

## Referencias

- [Plan de actualización](../plano-atualizacao.md) y [guía de uso](../uso.md#actualizar)
- [Sigstore trusted root](https://github.com/sigstore/root-signing/blob/main/targets/trusted_root.json)
- [RFC 6962](https://www.rfc-editor.org/rfc/rfc6962) y [DSSE](https://github.com/secure-systems-lab/dsse)
