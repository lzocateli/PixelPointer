#!/bin/sh
# Cenarios do install.sh contra um servidor local de artefatos.
# Executado por tools/scripts/Test-Installers.ps1 em containers limpos ou no macOS.
# Variaveis: INSTALLER (caminho do install.sh), BASE_URL (artefatos validos),
# TAMPERED_URL (mesmos pacotes com SHA256SUMS adulterado).
set -eu

installer="${INSTALLER:?INSTALLER nao definido}"
base_url="${BASE_URL:?BASE_URL nao definido}"
tampered_url="${TAMPERED_URL:?TAMPERED_URL nao definido}"

work="$(mktemp -d "${TMPDIR:-/tmp}/pixelpointer-installer-test.XXXXXX")"
trap 'rm -rf "$work"' EXIT

fail() {
    printf 'FALHA: %s\n' "$1" >&2
    if [ -f "$work/out" ]; then
        sed 's/^/  | /' "$work/out" >&2
    fi
    exit 1
}

pass() {
    printf 'ok: %s\n' "$1"
}

expect_code() {
    expected="$1"
    description="$2"
    shift 2
    set +e
    "$@" > "$work/out" 2>&1
    code=$?
    set -e
    if [ "$code" -ne "$expected" ]; then
        fail "$description: codigo $code, esperado $expected"
    fi
}

expect_code 0 "--help" sh "$installer" --help
grep -q -- '--prefix' "$work/out" || fail "--help sem a opcao --prefix"
grep -q -- '--uninstall' "$work/out" || fail "--help sem a opcao --uninstall"
grep -q -- '--purge' "$work/out" || fail "--help sem a opcao --purge"
pass "--help"

expect_code 2 "argumento desconhecido" sh "$installer" --bogus
expect_code 2 "--purge sem --uninstall" sh "$installer" --purge
expect_code 2 "http fora do loopback" sh "$installer" --base-url http://example.com/x
expect_code 2 "credenciais na URL" sh "$installer" --base-url http://localhost:80@example.com/x
expect_code 2 "prefixo relativo" sh "$installer" --base-url "$base_url" --prefix relativo
expect_code 2 "--global com --prefix" sh "$installer" --global --prefix "$work/x"
expect_code 2 "--base-url com --version" sh "$installer" --base-url "$base_url" --version 0.1.0
expect_code 2 "versao invalida" sh "$installer" --version '1.0.0;id'
pass "erros de uso retornam 2"

expect_code 1 "checksum adulterado" sh "$installer" --base-url "$tampered_url" --prefix "$work/tampered"
grep -q 'nao confere' "$work/out" || fail "checksum adulterado sem mensagem de divergencia"
[ ! -e "$work/tampered" ] || fail "checksum adulterado criou o diretorio de destino"
pass "checksum adulterado rejeitado sem efeitos"

expect_code 1 "versao inexistente no servidor" sh "$installer" --base-url "$base_url/inexistente" --prefix "$work/missing"
[ ! -e "$work/missing" ] || fail "download falho criou o diretorio de destino"
pass "download falho rejeitado sem efeitos"

prefix="$work/prefix"
expect_code 0 "instalacao em prefixo explicito" sh "$installer" --base-url "$base_url" --prefix "$prefix"
[ -x "$prefix/lib/pixelpointer/pixelpointer" ] || fail "executavel ausente ou sem permissao de execucao"
[ -s "$prefix/lib/pixelpointer/pixelpointer.svg" ] || fail "icone SVG ausente da instalacao"
[ -h "$prefix/bin/pixelpointer" ] || fail "link do comando ausente"
grep -q 'nao esta no PATH' "$work/out" || fail "aviso de PATH ausente"
receipt="$prefix/lib/pixelpointer/install-receipt.json"
[ -f "$receipt" ] || fail "recibo da instalacao ausente"
if [ "$(id -u)" -eq 0 ]; then expected_scope="global"; else expected_scope="user"; fi
grep -q "\"installer\": \"install.sh\", \"scope\": \"$expected_scope\"" "$receipt" || fail "recibo sem instalador ou escopo esperado: $(cat "$receipt")"
"$prefix/bin/pixelpointer" --version > /dev/null || fail "comando instalado nao executa"
"$prefix/bin/pixelpointer" selftest > /dev/null || fail "bibliotecas nativas nao carregam pelo link"
if [ "$(uname -s)" = "Linux" ]; then
    desktop_data="$work/desktop-data"
    expect_code 0 "integracao desktop apos instalar" env "XDG_DATA_HOME=$desktop_data" "$prefix/bin/pixelpointer" install-desktop-file
    [ -s "$desktop_data/applications/io.github.lzocateli.PixelPointer.desktop" ] || fail "entrada desktop ausente"
    cmp "$prefix/lib/pixelpointer/pixelpointer.svg" "$desktop_data/icons/hicolor/scalable/apps/io.github.lzocateli.PixelPointer.svg" || fail "icone desktop difere do instalado"
    expect_code 0 "remocao da integracao desktop" env "XDG_DATA_HOME=$desktop_data" "$prefix/bin/pixelpointer" uninstall-desktop-file
    [ ! -e "$desktop_data/icons/hicolor/scalable/apps/io.github.lzocateli.PixelPointer.svg" ] || fail "icone desktop permaneceu apos remover"
fi
pass "instalacao em prefixo explicito"

printf 'versao anterior\n' > "$prefix/lib/pixelpointer/pixelpointer"
printf 'residuo\n' > "$prefix/lib/pixelpointer/arquivo-antigo"
expect_code 0 "reinstalacao" env "PATH=$prefix/bin:$PATH" sh "$installer" --base-url "$base_url" --prefix "$prefix"
if grep -q 'nao esta no PATH' "$work/out"; then
    fail "aviso de PATH com o diretorio ja no PATH"
fi
"$prefix/bin/pixelpointer" --version > /dev/null || fail "reinstalacao nao substituiu o executavel"
[ -s "$prefix/lib/pixelpointer/pixelpointer.svg" ] || fail "reinstalacao perdeu o icone SVG"
[ ! -e "$prefix/lib/pixelpointer/arquivo-antigo" ] || fail "reinstalacao manteve arquivos da versao anterior"
[ "$(ls -A "$prefix/lib")" = "pixelpointer" ] || fail "diretorios temporarios restantes em $prefix/lib"
[ "$(ls -A "$prefix/bin")" = "pixelpointer" ] || fail "arquivos restantes em $prefix/bin"
pass "reinstalacao substitui o aplicativo sem residuos"

receipt_backup="$work/install-receipt.json"
cp "$prefix/lib/pixelpointer/install-receipt.json" "$receipt_backup"
rm -f -- "$prefix/lib/pixelpointer/install-receipt.json"
expect_code 1 "desinstalacao sem recibo" sh "$installer" --uninstall --prefix "$prefix"
[ -x "$prefix/lib/pixelpointer/pixelpointer" ] || fail "desinstalacao sem recibo removeu o aplicativo"
mv "$receipt_backup" "$prefix/lib/pixelpointer/install-receipt.json"

expect_code 0 "desinstalacao em prefixo explicito" sh "$installer" --uninstall --prefix "$prefix"
[ ! -e "$prefix/lib/pixelpointer" ] || fail "desinstalacao manteve o aplicativo"
[ ! -e "$prefix/bin/pixelpointer" ] || fail "desinstalacao manteve o link"
pass "desinstalacao remove somente o aplicativo"

foreign="$work/foreign-link"
mkdir -p "$foreign/bin"
ln -s /bin/true "$foreign/bin/pixelpointer"
expect_code 1 "link de outro aplicativo" sh "$installer" --base-url "$base_url" --prefix "$foreign"
[ "$(readlink "$foreign/bin/pixelpointer")" = "/bin/true" ] || fail "link de outro aplicativo alterado"
[ ! -e "$foreign/lib" ] || fail "instalacao recusada criou $foreign/lib"
pass "link de outro aplicativo recusado"

file_prefix="$work/foreign-file"
mkdir -p "$file_prefix/bin"
printf 'outro\n' > "$file_prefix/bin/pixelpointer"
expect_code 1 "arquivo existente no lugar do link" sh "$installer" --base-url "$base_url" --prefix "$file_prefix"
[ "$(cat "$file_prefix/bin/pixelpointer")" = "outro" ] || fail "arquivo existente alterado"
pass "arquivo existente no lugar do link recusado"

if [ "$(id -u)" -eq 0 ]; then
    expect_code 0 "--global como root" sh "$installer" --base-url "$base_url" --global
    [ -x /usr/local/lib/pixelpointer/pixelpointer ] || fail "--global nao instalou em /usr/local/lib/pixelpointer"
    [ -h /usr/local/bin/pixelpointer ] || fail "--global nao criou /usr/local/bin/pixelpointer"
    grep -q '"scope": "global"' /usr/local/lib/pixelpointer/install-receipt.json || fail "--global sem recibo de escopo global"
    pass "--global como root"

    mkdir -p "$work/owned/lib" "$work/shared/lib"
    chown 65534 "$work/owned/lib"
    chmod 0777 "$work/shared/lib"
    expect_code 1 "root em diretorio de outro usuario" sh "$installer" --base-url "$base_url" --prefix "$work/owned"
    grep -q 'nao pertence ao root' "$work/out" || fail "diretorio de outro usuario sem mensagem de dono"
    expect_code 1 "root em diretorio gravavel por outros" sh "$installer" --base-url "$base_url" --prefix "$work/shared"
    grep -q 'gravavel por grupo ou outros' "$work/out" || fail "diretorio gravavel sem mensagem de permissao"
    if [ -n "$(ls -A "$work/owned/lib")" ] || [ -n "$(ls -A "$work/shared/lib")" ]; then
        fail "instalacao recusada deixou arquivos"
    fi
    pass "root recusa diretorio controlado por outro usuario"
else
    expect_code 1 "--global sem root" sh "$installer" --base-url "$base_url" --global
    grep -q 'exige root' "$work/out" || fail "--global sem root sem mensagem de privilegio"
    pass "--global sem root recusado"

    expect_code 0 "prefixo padrao" sh "$installer" --base-url "$base_url"
    [ -x "$HOME/.local/lib/pixelpointer/pixelpointer" ] || fail "aplicativo ausente em \$HOME/.local/lib/pixelpointer"
    [ -h "$HOME/.local/bin/pixelpointer" ] || fail "link ausente em \$HOME/.local/bin"
    expect_code 0 "origem detectada pelo aplicativo" "$HOME/.local/bin/pixelpointer" update status
    grep -q 'Autoatualização ... disponível' "$work/out" || fail "aplicativo nao reconheceu a instalacao de usuario"
    pass "prefixo padrao \$HOME/.local"

    purge_data="$work/purge-data"
    purge_config="$work/purge-config"
    purge_state="$work/purge-state"
    purge_cache="$work/purge-cache"
    pictures="$work/pictures"
    mkdir -p "$work/bin" "$purge_data/Trash/files" "$purge_data/Trash/info" \
        "$purge_config/pixelpointer" "$purge_state/pixelpointer" "$purge_cache/pixelpointer/run" \
        "$pictures/Screenshots"
    # Keep these expressions literal for expansion when the fixture helper runs.
    # shellcheck disable=SC2016
    printf '%s\n' '#!/bin/sh' '[ "${1:-}" = PICTURES ] || exit 1' 'echo "$TEST_PICTURES_DIR"' > "$work/bin/xdg-user-dir"
    chmod 0755 "$work/bin/xdg-user-dir"
    printf 'config\n' > "$purge_config/pixelpointer/update.json"
    printf 'state\n' > "$purge_state/pixelpointer/screencast-restore-token"
    printf 'cache\n' > "$purge_cache/pixelpointer/run/pixelpointer.sock"
    printf 'capture\n' > "$pictures/Screenshots/pixelpointer-test.png"
    printf 'trash\n' > "$purge_data/Trash/files/pixelpointer-test.png"
    printf 'metadata\n' > "$purge_data/Trash/info/pixelpointer-test.png.trashinfo"
    printf 'keep\n' > "$purge_data/Trash/files/keep.txt"
    printf 'keep\n' > "$purge_data/Trash/info/keep.txt.trashinfo"
    env XDG_DATA_HOME="$purge_data" "$HOME/.local/bin/pixelpointer" install-desktop-file >/dev/null
    expect_code 0 "purge do usuario" env HOME="$HOME" XDG_CONFIG_HOME="$purge_config" \
        XDG_STATE_HOME="$purge_state" XDG_CACHE_HOME="$purge_cache" XDG_DATA_HOME="$purge_data" \
        TEST_PICTURES_DIR="$pictures" PATH="$work/bin:$PATH" sh "$installer" --uninstall --purge
    [ ! -e "$HOME/.local/lib/pixelpointer" ] || fail "purge manteve o aplicativo"
    [ ! -e "$purge_config/pixelpointer" ] || fail "purge manteve configuracoes"
    [ ! -e "$purge_state/pixelpointer" ] || fail "purge manteve estado"
    [ ! -e "$purge_cache/pixelpointer" ] || fail "purge manteve cache"
    [ ! -e "$pictures/Screenshots" ] || fail "purge manteve capturas"
    [ ! -e "$purge_data/Trash/files/pixelpointer-test.png" ] || fail "purge manteve item PixelPointer na Lixeira"
    [ ! -e "$purge_data/Trash/info/pixelpointer-test.png.trashinfo" ] || fail "purge manteve metadata PixelPointer na Lixeira"
    [ -f "$purge_data/Trash/files/keep.txt" ] || fail "purge removeu outro item da Lixeira"
    [ -f "$purge_data/Trash/info/keep.txt.trashinfo" ] || fail "purge removeu metadata alheia da Lixeira"
    pass "purge Linux remove dados do app e preserva outros itens da Lixeira"
fi

printf 'install.sh aprovado (uid %s)\n' "$(id -u)"
