#!/bin/sh
# Instalador do pixelpointer para Linux e macOS.
# Uso: curl -fsSL https://github.com/lzocateli/PixelPointer/releases/latest/download/install.sh | sh
#      sh install.sh --help
set -eu

repository_url="https://github.com/lzocateli/PixelPointer/releases"
version="latest"
prefix=""
global=0
base_url=""
work_dir=""
staged_dir=""
backup_dir=""
uninstall=0
purge=0

usage() {
    cat <<'EOF'
Instala ou remove o pixelpointer (Native AOT, sem dependencia de .NET). A
instalacao vem das releases do GitHub e confere o SHA-256 com o SHA256SUMS.

O aplicativo (executavel e bibliotecas nativas SDL3 e libSkiaSharp) vai para
<prefixo>/lib/pixelpointer e o comando e um link em <prefixo>/bin/pixelpointer.

Uso:
  install.sh [--version <X.Y.Z|latest>] [--prefix <dir> | --global]
    install.sh --uninstall [--prefix <dir> | --global] [--purge]
  install.sh --help

Opcoes:
  --version <v>    Versao a instalar (X.Y.Z ou X.Y.Z-sufixo). Padrao: latest.
  --prefix <dir>   Prefixo absoluto. Padrao: $HOME/.local.
  --global         Usa o prefixo /usr/local. Exige root (use sudo); o
                   instalador nunca se autoeleva.
    --uninstall      Remove a instalacao. Para --global no Linux, remova antes
                                     a integracao desktop e encerre o daemon como usuario.
    --purge          Com --uninstall, remove configuracoes, cache, capturas e
                                     itens PixelPointer da Lixeira (Linux, usuario).
  --base-url <url> Diretorio com os pacotes e o SHA256SUMS (espelho ou
                   testes). Exige https://, exceto http://127.0.0.1 e
                   http://localhost. Nao combina com --version.
  --help           Mostra esta ajuda.

Requisitos de instalacao: curl ou wget, tar com gzip e sha256sum ou shasum.
--purge no Linux tambem exige xdg-user-dir.
Plataformas: linux-x64 e linux-arm64 (glibc 2.34+ e libfontconfig),
osx-arm64 e osx-x64.

Grava <prefixo>/lib/pixelpointer/install-receipt.json. O comando
'pixelpointer update' usa esse recibo para reconhecer uma instalacao de usuario,
a unica que o proprio aplicativo pode atualizar.

O instalador nao altera perfis de shell nem o PATH: se <prefixo>/bin nao
estiver no PATH, ele mostra a linha a adicionar. No Linux, rode depois
'pixelpointer install-desktop-file' e, no GNOME, 'pixelpointer grant'.

Exemplos:
  sh install.sh
  sh install.sh --version 0.1.0 --prefix "$HOME/apps"
  sh install.sh --uninstall --purge
  curl -fsSL https://github.com/lzocateli/PixelPointer/releases/latest/download/install.sh | sudo sh -s -- --global

Codigos de saida: 0 sucesso; 1 falha; 2 erro de uso.
EOF
}

die() {
    printf 'pixelpointer-install: %s\n' "$1" >&2
    exit 1
}

usage_error() {
    printf 'pixelpointer-install: %s. Use --help.\n' "$1" >&2
    exit 2
}

cleanup() {
    if [ -n "$staged_dir" ] && [ -d "$staged_dir" ]; then
        rm -rf "$staged_dir"
    fi
    if [ -n "$work_dir" ] && [ -d "$work_dir" ]; then
        rm -rf "$work_dir"
    fi
}

require_value() {
    if [ $# -lt 2 ] || [ -z "$2" ]; then
        usage_error "a opcao $1 exige um valor"
    fi
}

# Como root, um diretorio controlado por outro usuario permitiria trocar os arquivos
# temporarios por links simbolicos e sobrescrever arquivos do sistema.
check_root_directory() {
    if [ "$(id -u)" -ne 0 ]; then
        return
    fi
    # shellcheck disable=SC2012 # ls -ldn e a forma POSIX de obter dono e modo.
    dir_info="$(ls -ldn "$1")"
    dir_mode="$(printf '%s\n' "$dir_info" | awk '{ print $1 }')"
    dir_owner="$(printf '%s\n' "$dir_info" | awk '{ print $3 }')"
    case "$dir_mode" in
        ?????w????*|????????w?*) die "$1 e gravavel por grupo ou outros; instalacao como root recusada" ;;
    esac
    if [ "$dir_owner" != "0" ]; then
        die "$1 nao pertence ao root; instale sem sudo com --prefix"
    fi
}

version_set=0
while [ $# -gt 0 ]; do
    case "$1" in
        --help|-h)
            usage
            exit 0
            ;;
        --version)
            require_value "$@"
            version="$2"
            version_set=1
            shift 2
            ;;
        --version=*)
            version="${1#*=}"
            version_set=1
            shift
            ;;
        --prefix)
            require_value "$@"
            prefix="$2"
            shift 2
            ;;
        --prefix=*)
            prefix="${1#*=}"
            shift
            ;;
        --global)
            global=1
            shift
            ;;
        --uninstall)
            uninstall=1
            shift
            ;;
        --purge)
            purge=1
            shift
            ;;
        --base-url)
            require_value "$@"
            base_url="$2"
            shift 2
            ;;
        --base-url=*)
            base_url="${1#*=}"
            shift
            ;;
        *)
            usage_error "argumento desconhecido: $1"
            ;;
    esac
done

if [ "$purge" -eq 1 ] && [ "$uninstall" -ne 1 ]; then
    usage_error "--purge exige --uninstall"
fi
if [ "$uninstall" -eq 1 ] && { [ "$version_set" -eq 1 ] || [ -n "$base_url" ]; }; then
    usage_error "--uninstall nao combina com --version ou --base-url"
fi

version="${version#v}"
if [ "$version" != "latest" ]; then
    case "$version" in
        [0-9]*.[0-9]*.[0-9]*) ;;
        *) usage_error "versao invalida: $version" ;;
    esac
    case "$version" in
        *[!0-9A-Za-z.-]*) usage_error "versao invalida: $version" ;;
    esac
fi

if [ "$global" -eq 1 ] && [ -n "$prefix" ]; then
    usage_error "--global e --prefix sao mutuamente exclusivos"
fi

secure=1
if [ -n "$base_url" ]; then
    if [ "$version_set" -eq 1 ]; then
        usage_error "--base-url e --version sao mutuamente exclusivos"
    fi
    base_url="${base_url%/}"
    case "$base_url" in
        https://?*) ;;
        http://?*)
            host_port="${base_url#http://}"
            host_port="${host_port%%/*}"
            case "$host_port" in
                *@*) usage_error "--base-url nao aceita credenciais" ;;
            esac
            case "${host_port%:*}" in
                127.0.0.1|localhost) secure=0 ;;
                *) usage_error "--base-url http:// so e aceito para 127.0.0.1 ou localhost" ;;
            esac
            ;;
        *) usage_error "--base-url deve usar https://" ;;
    esac
    asset_url="$base_url"
elif [ "$version" = "latest" ]; then
    asset_url="$repository_url/latest/download"
else
    asset_url="$repository_url/download/v$version"
fi

if [ "$global" -eq 1 ]; then
    if [ "$(id -u)" -ne 0 ]; then
        die "--global exige root; execute com sudo (o instalador nao se autoeleva)"
    fi
    prefix="/usr/local"
elif [ -z "$prefix" ]; then
    if [ -z "${HOME:-}" ]; then
        die "HOME nao definido; informe --prefix"
    fi
    prefix="$HOME/.local"
fi

case "$prefix" in
    /?*) ;;
    *) usage_error "--prefix deve ser um caminho absoluto" ;;
esac
prefix="${prefix%/}"

os_name="$(uname -s)"
machine="$(uname -m)"
case "$machine" in
    x86_64|amd64) arch="x64" ;;
    aarch64|arm64) arch="arm64" ;;
    *) die "arquitetura nao suportada: $machine" ;;
esac

case "$os_name" in
    Linux)
        for loader in /lib/ld-musl-*.so.1; do
            if [ -e "$loader" ]; then
                die "distribuicoes musl (como Alpine) nao sao suportadas: o SDL3 distribuido exige glibc 2.34+"
            fi
        done
        rid="linux-$arch"
        libraries="libSDL3.so libSkiaSharp.so"
        ;;
    Darwin)
        # Shell x64 sob Rosetta reporta x86_64 em Mac Apple Silicon.
        if [ "$arch" = "x64" ] && [ "$(sysctl -n sysctl.proc_translated 2>/dev/null || echo 0)" = "1" ]; then
            arch="arm64"
        fi
        rid="osx-$arch"
        libraries="libSDL3.dylib libSkiaSharp.dylib libPixelPointerScreenCaptureKit.dylib"
        ;;
    *)
        die "sistema operacional nao suportado: $os_name (no Windows, use install.ps1)"
        ;;
esac

if [ "$purge" -eq 1 ]; then
    if [ "$os_name" != "Linux" ]; then
        usage_error "--purge so esta disponivel no Linux"
    fi
    if [ "$global" -eq 1 ] || [ "$(id -u)" -eq 0 ]; then
        usage_error "--purge exige instalacao e execucao no usuario; nao use sudo ou --global"
    fi
    for path in "${XDG_CONFIG_HOME:-$HOME/.config}" "${XDG_STATE_HOME:-$HOME/.local/state}" \
        "${XDG_CACHE_HOME:-$HOME/.cache}" "${XDG_DATA_HOME:-$HOME/.local/share}"; do
        case "$path" in
            /*) [ "$path" != "/" ] || die "diretorio XDG invalido para --purge" ;;
            *) die "diretorios XDG devem ser absolutos para --purge" ;;
        esac
    done
    if ! command -v xdg-user-dir >/dev/null 2>&1; then
        die "--purge exige xdg-user-dir (pacote xdg-user-dirs) para localizar as capturas"
    fi
    pictures_dir="$(xdg-user-dir PICTURES)" || die "nao foi possivel resolver PICTURES"
    case "$pictures_dir" in
        /*)
            if [ "$pictures_dir" = "/" ] || [ "$pictures_dir" = "$HOME" ]; then
                die "diretorio PICTURES invalido para --purge"
            fi
            ;;
        *) die "xdg-user-dir nao retornou um caminho absoluto para PICTURES" ;;
    esac
fi

asset="pixelpointer-$rid.tar.gz"
lib_root="$prefix/lib"
bin_dir="$prefix/bin"
app_dir="$lib_root/pixelpointer"
link="$bin_dir/pixelpointer"

if [ "$uninstall" -eq 1 ]; then
    if [ -L "$app_dir" ] || [ ! -d "$app_dir" ] || [ -L "$app_dir/pixelpointer" ] || [ ! -f "$app_dir/pixelpointer" ]; then
        die "instalacao PixelPointer nao encontrada ou caminho inesperado em $app_dir"
    fi
    receipt="$app_dir/install-receipt.json"
    if [ -L "$receipt" ] || [ ! -f "$receipt" ] || ! grep -Eq '"installer"[[:space:]]*:[[:space:]]*"install\.sh"' "$receipt"; then
        die "recibo install.sh ausente ou invalido em $app_dir; reinstale pelo instalador antes de remover"
    fi
    if [ -L "$link" ]; then
        current_target="$(readlink "$link")"
        if [ "$current_target" != "$app_dir/pixelpointer" ]; then
            die "$link aponta para $current_target; nenhum arquivo foi removido"
        fi
    elif [ -e "$link" ]; then
        die "$link nao e um link do PixelPointer; nenhum arquivo foi removido"
    fi
    "$app_dir/pixelpointer" --version >/dev/null || die "executavel em $app_dir nao e um PixelPointer valido"
    if [ "$global" -eq 1 ]; then
        if [ "$os_name" = "Linux" ]; then
            printf 'Aviso: daemon e integracao desktop do usuario nao foram alterados; remova-os antes de executar com sudo.\n' >&2
        fi
    else
        if "$app_dir/pixelpointer" status >/dev/null 2>&1; then
            "$app_dir/pixelpointer" quit >/dev/null || die "nao foi possivel encerrar o daemon; nada foi removido"
            attempts=0
            while "$app_dir/pixelpointer" status >/dev/null 2>&1; do
                attempts=$((attempts + 1))
                if [ "$attempts" -ge 10 ]; then
                    die "o daemon nao encerrou a tempo; nada foi removido"
                fi
                sleep 1
            done
        fi
        if [ "$os_name" = "Linux" ]; then
            "$app_dir/pixelpointer" uninstall-desktop-file || die "nao foi possivel remover a integracao desktop; nenhum binario foi removido"
        fi
        if [ -n "${XDG_RUNTIME_DIR:-}" ] && [ -S "$XDG_RUNTIME_DIR/pixelpointer.sock" ] && [ ! -L "$XDG_RUNTIME_DIR/pixelpointer.sock" ]; then
            rm -f -- "$XDG_RUNTIME_DIR/pixelpointer.sock"
        fi
    fi

    rm -f -- "$link"
    rm -rf -- "$app_dir"
    if [ "$purge" -eq 1 ]; then
        config_home="${XDG_CONFIG_HOME:-$HOME/.config}"
        state_home="${XDG_STATE_HOME:-$HOME/.local/state}"
        cache_home="${XDG_CACHE_HOME:-$HOME/.cache}"
        data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
        rm -rf -- "$config_home/pixelpointer" "$state_home/pixelpointer" "$cache_home/pixelpointer" "$pictures_dir/Screenshots"
        for trash_dir in "$data_home/Trash/files" "$data_home/Trash/info"; do
            if [ -d "$trash_dir" ] && [ ! -L "$trash_dir" ]; then
                find "$trash_dir" -mindepth 1 -maxdepth 1 -iname '*pixelpointer*' -exec rm -rf -- {} +
            fi
        done
    fi
    printf 'PixelPointer removido de %s\n' "$prefix"
    if [ "$purge" -eq 1 ]; then
        printf 'Configuracoes, cache, capturas e itens PixelPointer da Lixeira removidos.\n'
    fi
    exit 0
fi

download() {
    if command -v curl >/dev/null 2>&1; then
        if [ "$secure" -eq 1 ]; then
            curl --proto '=https' --proto-redir '=https' -fsSL --retry 3 -o "$2" "$1"
        else
            curl -fsSL -o "$2" "$1"
        fi
    elif command -v wget >/dev/null 2>&1; then
        wget -q -O "$2" "$1"
    else
        die "curl ou wget e necessario"
    fi
}

sha256_of() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | cut -d ' ' -f 1
    elif command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | cut -d ' ' -f 1
    else
        die "sha256sum ou shasum e necessario"
    fi
}

trap cleanup EXIT
trap 'exit 130' HUP INT TERM

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/pixelpointer-install.XXXXXX")" || die "falha ao criar diretorio temporario"

printf 'Baixando %s de %s\n' "$asset" "$asset_url"
download "$asset_url/SHA256SUMS" "$work_dir/SHA256SUMS" || die "falha ao baixar SHA256SUMS de $asset_url"
download "$asset_url/$asset" "$work_dir/$asset" || die "falha ao baixar $asset (versao ou plataforma inexistente?)"

expected="$(awk -v name="$asset" '$2 == name { count++; hash = $1 } END { if (count == 1) print hash }' "$work_dir/SHA256SUMS")"
case "$expected" in
    ''|*[!0-9a-f]*) die "SHA256SUMS nao contem uma entrada valida para $asset" ;;
esac
if [ "${#expected}" -ne 64 ]; then
    die "SHA256SUMS nao contem uma entrada valida para $asset"
fi

actual="$(sha256_of "$work_dir/$asset")"
if [ "$actual" != "$expected" ]; then
    die "SHA-256 de $asset nao confere (esperado $expected, obtido $actual); nada foi instalado"
fi
printf 'SHA-256 conferido: %s\n' "$actual"

mkdir "$work_dir/extract"
tar -xzf "$work_dir/$asset" -C "$work_dir/extract" || die "falha ao extrair $asset"
for name in pixelpointer $libraries pixelpointer.svg LICENSE THIRD-PARTY-NOTICES.md; do
    if [ ! -f "$work_dir/extract/$name" ] || [ -h "$work_dir/extract/$name" ]; then
        die "$asset nao contem $name"
    fi
done

# Nunca substitui arquivos que nao foram criados por este instalador.
if [ -h "$link" ]; then
    current_target="$(readlink "$link")"
    if [ "$current_target" != "$app_dir/pixelpointer" ]; then
        die "$link aponta para $current_target; remova-o antes de instalar"
    fi
elif [ -e "$link" ]; then
    die "$link existe e nao e um link do pixelpointer; remova-o antes de instalar"
fi
if [ -h "$app_dir" ] || { [ -e "$app_dir" ] && [ ! -d "$app_dir" ]; }; then
    die "$app_dir nao e um diretorio do pixelpointer; remova-o antes de instalar"
fi
if [ -d "$app_dir" ] && [ -n "$(ls -A "$app_dir")" ] && [ ! -f "$app_dir/pixelpointer" ]; then
    die "$app_dir nao esta vazio e nao contem o pixelpointer; remova-o antes de instalar"
fi

mkdir -p "$lib_root" "$bin_dir" || die "falha ao criar $lib_root ou $bin_dir"
check_root_directory "$lib_root"
check_root_directory "$bin_dir"

staged_dir="$(mktemp -d "$lib_root/.pixelpointer.install.XXXXXX")" || die "falha ao criar diretorio temporario em $lib_root"
for name in pixelpointer $libraries pixelpointer.svg LICENSE THIRD-PARTY-NOTICES.md; do
    cp "$work_dir/extract/$name" "$staged_dir/$name" || die "falha ao copiar para $lib_root"
    chmod 0644 "$staged_dir/$name"
done
# Recibo lido pelo atualizador do aplicativo: so instalacoes de usuario aceitam autoatualizacao.
receipt_scope="user"
if [ "$global" -eq 1 ] || [ "$(id -u)" -eq 0 ]; then
    receipt_scope="global"
fi
printf '{"version": 1, "installer": "install.sh", "scope": "%s", "rid": "%s"}\n' "$receipt_scope" "$rid" > "$staged_dir/install-receipt.json" \
    || die "falha ao gravar o recibo da instalacao"
chmod 0644 "$staged_dir/install-receipt.json"
chmod 0755 "$staged_dir" "$staged_dir/pixelpointer"

# Executa no destino (nao em /tmp, que pode ser noexec) antes de substituir a versao atual.
installed_version="$("$staged_dir/pixelpointer" --version)" || die "o binario baixado nao executa nesta plataforma ($rid); nada foi substituido"
if ! "$staged_dir/pixelpointer" selftest >/dev/null 2>"$work_dir/selftest.err"; then
    sed 's/^/  | /' "$work_dir/selftest.err" >&2
    die "as bibliotecas nativas nao carregam nesta maquina (Linux exige glibc 2.34+ e libfontconfig); nada foi substituido"
fi

if [ -d "$app_dir" ]; then
    backup_dir="$lib_root/.pixelpointer.old.$$"
    rm -rf "$backup_dir"
    mv "$app_dir" "$backup_dir" || die "falha ao mover a versao atual de $app_dir"
fi
if ! mv "$staged_dir" "$app_dir"; then
    if [ -n "$backup_dir" ]; then
        mv "$backup_dir" "$app_dir"
    fi
    die "falha ao instalar em $app_dir"
fi
staged_dir=""
if [ -n "$backup_dir" ]; then
    rm -rf "$backup_dir"
fi

ln -sfn "$app_dir/pixelpointer" "$link" || die "falha ao criar o link $link"

printf 'pixelpointer %s instalado em %s (comando: %s)\n' "$installed_version" "$app_dir" "$link"

case ":${PATH:-}:" in
    *":$bin_dir:"*) ;;
    *)
        printf '\nAviso: %s nao esta no PATH. Adicione ao perfil do seu shell:\n' "$bin_dir"
        printf "  export PATH=\"%s:\$PATH\"\n" "$bin_dir"
        ;;
esac

if [ "$os_name" = "Linux" ]; then
    printf '\nProximos passos: pixelpointer install-desktop-file && pixelpointer grant && pixelpointer daemon\n'
else
    printf '\nProximo passo: pixelpointer daemon (conceda a permissao de Gravacao de Tela quando o macOS pedir)\n'
fi
