#!/bin/sh
# Executa dentro do container: sobe sway headless, PipeWire, WirePlumber e os portais e valida a captura
# ScreenCast do pacote em /package. Evidências em /evidence; código 0 somente se todas as asserções passarem.
set -eu

evidence=/evidence
log() { printf '%s %s\n' "$(date -u +%H:%M:%S)" "$*" | tee -a "$evidence/timeline.log"; }
fail() { log "FALHA: $*"; printf '{"passed": false, "error": "%s"}\n' "$*" > "$evidence/result.json"; exit 1; }
wait_for() {
  description=$1; shift
  attempts=0
  until "$@" >/dev/null 2>&1; do
    attempts=$((attempts + 1))
    [ "$attempts" -le 100 ] || fail "prazo excedido: $description"
    sleep 0.1
  done
  log "pronto: $description"
}
first_socket() {
  for candidate in "$XDG_RUNTIME_DIR"/$1; do
    if [ -S "$candidate" ]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  return 1
}

mkdir -p "$HOME" "$XDG_CONFIG_HOME/xdg-desktop-portal" "$XDG_STATE_HOME" "$XDG_RUNTIME_DIR"
chmod 700 "$XDG_RUNTIME_DIR"
printf '[preferred]\ndefault=wlr\n' > "$XDG_CONFIG_HOME/xdg-desktop-portal/sway-portals.conf"

dbus-daemon --session --nofork --address="unix:path=$XDG_RUNTIME_DIR/bus" > "$evidence/dbus.log" 2>&1 &
export DBUS_SESSION_BUS_ADDRESS="unix:path=$XDG_RUNTIME_DIR/bus"
wait_for "D-Bus de sessão" test -S "$XDG_RUNTIME_DIR/bus"

sway -c /opt/wayland/sway.conf > "$evidence/sway.log" 2>&1 &
wait_for "sway headless" first_socket 'wayland-[0-9]'
WAYLAND_DISPLAY=$(basename "$(first_socket 'wayland-[0-9]')")
export WAYLAND_DISPLAY

pipewire > "$evidence/pipewire.log" 2>&1 &
wait_for "PipeWire" test -S "$XDG_RUNTIME_DIR/pipewire-0"
wireplumber > "$evidence/wireplumber.log" 2>&1 &
# O portal Registry exige a entrada desktop do app id antes da primeira chamada.
/package/pixelpointer install-desktop-file > "$evidence/desktop.txt" 2>&1 || fail "install-desktop-file"
/usr/libexec/xdg-desktop-portal-wlr -l DEBUG -c /opt/wayland/xdpw.conf > "$evidence/xdpw.log" 2>&1 &
/usr/libexec/xdg-desktop-portal -r > "$evidence/portal.log" 2>&1 &
wait_for "portal ScreenCast" dbus-send --session --print-reply --dest=org.freedesktop.portal.Desktop \
  /org/freedesktop/portal/desktop org.freedesktop.DBus.Properties.Get \
  string:org.freedesktop.portal.ScreenCast string:version

/package/pixelpointer probe > "$evidence/probe.txt" 2>&1 || true

if [ "${EXPECT:-screencast}" = unavailable ]; then
  # Sem nó de renderização o xdg-desktop-portal-wlr recusa o ScreenCast (exige DMA-BUF) e não há Screenshot:
  # o comando deve falhar com mensagem clara, sem abortar o processo nem gravar arquivo.
  status=0
  /package/pixelpointer capture --out "$evidence/unavailable.png" > "$evidence/unavailable.out" \
    2> "$evidence/unavailable.err" || status=$?
  [ "$status" = 1 ] || fail "código inesperado sem ScreenCast: $status"
  grep -q 'ScreenCast indisponível' "$evidence/unavailable.err" || fail "retorno ao Screenshot não informado"
  grep -q 'Nenhum portal de captura disponível' "$evidence/unavailable.err" || fail "erro final não informado"
  ! grep -q 'Unhandled exception' "$evidence/unavailable.err" || fail "exceção não tratada"
  [ ! -e "$evidence/unavailable.png" ] || fail "arquivo gravado sem captura"
  printf '{"passed": true, "mode": "unavailable"}\n' > "$evidence/result.json"
  log "aprovado: indisponibilidade tratada"
  exit 0
fi

capture() {
  name=$1
  expected=$2
  /package/pixelpointer capture --out "$evidence/$name.png" > "$evidence/$name.out" 2> "$evidence/$name.err" \
    || fail "capture $name terminou com erro"
  grep -q 'ScreenCast + PipeWire: 800x600' "$evidence/$name.out" || fail "capture $name não usou ScreenCast 800x600"
  grep -q '^ScreenCast: portal' "$evidence/$name.err" || fail "capture $name sem medição de latência"
  pixel=$(magick "$evidence/$name.png" -format '%[pixel:p{400,300}]' info:)
  case "$pixel" in
    "srgb($expected)"|"srgba($expected,1)") ;;
    *) fail "capture $name com cor inesperada: $pixel (esperado $expected)" ;;
  esac
  log "ok: $name $(grep '^ScreenCast' "$evidence/$name.err")"
}

capture first '0,170,204'
token="$XDG_STATE_HOME/pixelpointer/screencast-restore-token"
if [ -e "$token" ]; then
  mode=$(stat -c %a "$token")
  [ "$mode" = 600 ] || fail "restore token com permissão $mode"
  log "ok: restore token salvo com permissão 600"
else
  log "informação: o backend não devolveu restore token"
fi
# wlroots só envia quadro após dano: muda o fundo e exige a cor nova, provando quadro atual e restore token.
SWAYSOCK=$(first_socket 'sway-ipc.*.sock') || fail "socket IPC do sway ausente"
export SWAYSOCK
swaymsg output HEADLESS-1 bg '#cc5500' solid_color > /dev/null || fail "swaymsg"
capture second '204,85,0'

if grep -q 'Screenshot' "$evidence/first.err" "$evidence/second.err"; then
  fail "a captura recorreu ao Screenshot"
fi

printf '{"passed": true, "mode": "screencast"}\n' > "$evidence/result.json"
log "aprovado"
