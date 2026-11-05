# Dos cuentas corporativas en GitHub y Copilot

[Português (Brasil)](../contas-github-copilot.md) · [English](../en/contas-github-copilot.md) · **Español**

Este tutorial ayuda a los colaboradores a utilizar cuentas de dos organizaciones
sin confundir la licencia de Copilot, el acceso al repositorio y la autoría de
commits. Los ejemplos son ficticios. Ningún comando de esta guía realiza commits,
push ni despliegues.

## Requisitos y limitaciones

- VS Code actualizado, Git y GitHub CLI (`gh`) instalados.
- Acceso autorizado a los repositorios y a Copilot en cada cuenta.
- Permiso de la empresa para utilizar IA con el código del proyecto.
- Las instrucciones del CLI suponen dos cuentas en `github.com`.
  Las cuentas de otros hosts requieren autenticación y selección de ese host.
- Utilice el flujo de autenticación aprobado por la empresa, incluido SSO cuando
  sea necesario. Nunca guarde tokens en el repositorio ni los incluya en comandos
  o logs.

| Configuración | Qué controla |
|---|---|
| Cuenta de Copilot en VS Code | Licencia, consumo y políticas de Copilot |
| Cuenta de GitHub Pull Requests | Acceso de la extensión a PRs e issues |
| Cuenta de Git/`gh` | Acceso a repositorios y operaciones remotas |
| `user.name` y `user.email` de Git | Autoría de commits, no autenticación |

Cambiar una configuración no cambia automáticamente las demás. El propietario del
repositorio no selecciona la licencia de Copilot; `gh auth status` no indica qué
cuenta utiliza Copilot.

## Seleccionar Copilot por proyecto

En VS Code, con el proyecto abierto:

1. Abra **Accounts / Cuentas** en la barra de actividades.
2. Seleccione **Manage Extension Account Preferences**.
3. Elija **GitHub Copilot Chat**.
4. Seleccione la cuenta autorizada por la organización responsable del código.
5. Si la cuenta no aparece, añádala mediante el inicio de sesión de GitHub y repita
   la selección. Compruebe el usuario del navegador antes de autorizar.

La preferencia se guarda por workspace y perfil. Repita en el proyecto de la otra
organización, eligiendo la otra cuenta. Compruebe la preferencia seleccionada antes
de enviar contenido al chat o activar sugerencias.

Configure también la preferencia de **GitHub Pull Requests**, si aparece en la
lista, para la cuenta con acceso al repositorio. Esto no cambia la autenticación
del terminal.

Para cuentas de `GHE.com`, siga la configuración específica del proveedor de
autenticación en la [documentación oficial de VS Code](https://code.visualstudio.com/docs/setup/copilot#_use-a-different-github-account-per-workspace-or-profile);
no suponga que el flujo de `github.com` se aplica sin cambios.

## Separar perfiles de VS Code

En **Manage → Profiles → Create Profile**, cree perfiles como **Empresa A** y
**Empresa B**. Abra cada workspace en el perfil correspondiente y seleccione la
cuenta de Copilot según la sección anterior.

Los perfiles organizan configuraciones y extensiones, pero no son una barrera de
seguridad ni aíslan automáticamente las credenciales del terminal.

## Conectar y alternar cuentas en GitHub CLI

Ejecute en el terminal local, desde cualquier directorio. Autentique cada cuenta
en el navegador, repitiendo el comando y comprobando el usuario autorizado:

```bash
gh auth login --hostname github.com --git-protocol https --web
```

El resultado esperado es una cuenta añadida al almacenamiento de credenciales del
CLI. El inicio de sesión no cambia la cuenta de Copilot. Para elegir la cuenta
activa:

```bash
gh auth switch --hostname github.com --user USUARIO_EMPRESA_A
```

Para la otra empresa, sustituya por `USUARIO_EMPRESA_B`. Compruebe la cuenta activa
con `gh auth status`, sin publicar su salida completa en tickets o logs.

**El cambio no es por proyecto:** modifica la cuenta activa de `gh` en ese
entorno. Evite alternar mientras haya operaciones de otra cuenta en ejecución.

## Diferenciar HTTPS, SSH y autoría de Git

En la raíz del clon, `git remote -v` muestra el transporte configurado; no publique
URLs con credenciales.

- **HTTPS:** `gh auth setup-git` configura Git para utilizar el helper de
  credenciales de `gh`. Modifica la configuración de Git del usuario; ejecútelo
  solo si la empresa lo permite. Un helper o regla por URL existente puede
  influir en la cuenta utilizada; compruebe la configuración antes de operar
  con el remoto.
- **SSH:** `gh auth switch` no cambia claves SSH. Para dos identidades, utilice
  una clave y un alias de host por cuenta, según la
  [guía oficial de GitHub](https://docs.github.com/en/authentication/connecting-to-github-with-ssh).
  Configure cada remoto con el alias adecuado y no comparta claves privadas.

### SSH con múltiples cuentas y organizaciones

En el archivo de configuración SSH del usuario (Linux/macOS: `~/.ssh/config`;
Windows con OpenSSH: `%USERPROFILE%\.ssh\config`), adapte los bloques existentes
sin sobrescribir otras entradas. Ejemplo ficticio:

```sshconfig
Host github.com
  HostName github.com
  User git
  IdentityFile ~/.ssh/id_ed25519_github
  IdentitiesOnly yes

Host github-pessoal
  HostName github.com
  User git
  IdentityFile ~/.ssh/id_ed25519_github_pessoal
  IdentitiesOnly yes

Host github-empresa-b
  HostName github.com
  User git
  IdentityFile ~/.ssh/id_ed25519_github_empresa_b
  IdentitiesOnly yes
```

En GitHub, el usuario SSH siempre es **`git`**, no el login de su cuenta.
La clave pública registrada en GitHub determina la cuenta autenticada.
`Host` es el alias local que selecciona la configuración; `HostName` es el servidor
real. `IdentitiesOnly yes` limita las identidades ofrecidas a las configuradas,
evitando que el agente ofrezca claves no relacionadas. Otras entradas aplicables,
incluido `Host *`, pueden añadir identidades; revise configuraciones superpuestas.
Nunca incluya el contenido de una clave privada en este archivo.

Registre cada clave pública en la cuenta correspondiente y autorícela para SSO
en la organización cuando sea necesario. Una cuenta con acceso a varias
organizaciones puede utilizar la misma clave autorizada: los aliases separan
identidades, no conceden permisos.

En el terminal local, desde cualquier directorio, pruebe la identidad:

```bash
ssh -T git@github-pessoal
ssh -T git@github-empresa-b
```

En la primera conexión, compare la fingerprint del servidor con las
[fingerprints oficiales de GitHub](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/githubs-ssh-key-fingerprints)
antes de aceptar. La respuesta esperada identifica al usuario: `Hi USERNAME! ...`.
GitHub no ofrece shell y puede devolver código `1` incluso tras autenticar
correctamente; compruebe el mensaje, no solo el código.

Para clonar con el alias personal, ejecute en el directorio padre deseado:

```bash
git clone -b main git@github-pessoal:CONTA_EXEMPLO/REPOSITORIO_EXEMPLO.git
```

No utilice `CONTA_EXEMPLO@github-pessoal:...`: sustituiría `User git` por el nombre
de la cuenta e impediría la autenticación esperada.
El propietario después de `:` puede ser una cuenta u organización; no selecciona
la identidad SSH.

Para cambiar el remoto de un clon existente, ejecute en la raíz del clon,
sustituyendo los placeholders y conservando el propietario y repositorio actuales:

```bash
git remote -v
git remote set-url origin git@github-pessoal:CONTA_EXEMPLO/REPOSITORIO_EXEMPLO.git
git remote -v
git ls-remote origin HEAD
```

`set-url` cambia la URL local, sin push ni cambios de historial. La lista debe
mostrar el alias correcto para fetch y push; `ls-remote` confirma acceso de lectura,
no permiso de escritura. Si existe una URL de push separada, compruébela con
`git config --get-all remote.origin.pushurl` y actualice cada entrada necesaria
con `git remote set-url --push origin NUEVA_URL URL_ANTIGUA`.
Para deshacer, restaure la URL anterior con `git remote set-url origin URL_ANTIGUA`
y, si las modificó, las URLs de push.

### Autoría local de commits

Para definir la autoría solo en este repositorio, ejecute en la raíz del clon:

```bash
git config --local user.name "Su Nombre"
git config --local user.email "su-correo-corporativo@example.com"
```

Estos comandos solo modifican la configuración local del clon. Compruebe los
valores con `git config --local --get user.name` y
`git config --local --get user.email`. No conceden acceso a GitHub.

## Comprobaciones antes de trabajar

- Proyecto A: Copilot A, acceso GitHub A y correo Git A.
- Proyecto B: Copilot B, acceso GitHub B y correo Git B.
- Compruebe la cuenta de Copilot en las preferencias de la extensión y la del CLI
  por separado.
- No utilice la licencia de otra empresa para enviar código sin autorización.
- No incluya credenciales ni selectores de identidad personal en las instrucciones
  versionadas de Copilot; la selección corresponde a la autenticación del editor.

Los nombres de los menús pueden variar según la versión de VS Code. Esta guía no
representa una validación con dos cuentas corporativas reales ni con políticas
específicas de una organización.
