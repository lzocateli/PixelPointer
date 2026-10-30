# Guia de uso

**Português (Brasil)** · [English](en/usage.md) · [Español](es/uso.md)

## Comandos

| Comando | Função |
| --- | --- |
| `pixelpointer daemon` | Fica em execução, registra os atalhos globais (Ctrl+1 zoom, Ctrl+2 desenho) e o canal local de comandos; Ctrl+C ou SIGTERM encerra |
| `pixelpointer zoom` | Abre o zoom agora; com o `daemon` em execução, envia o comando a ele |
| `pixelpointer draw` | Abre o desenho sem zoom; mesma regra do `zoom` |
| `pixelpointer overlay [--image arq.png] [--mode zoom\|draw]` | Abre a sobreposição sobre uma captura nova ou sobre uma imagem |
| `pixelpointer capture [--out arq.png]` | Captura a tela e informa a latência |
| `pixelpointer probe` | Diagnóstico: SO, sessão, monitores, versões dos portais |
| `pixelpointer install-desktop-file` | Linux: instala o `.desktop` com o app id `io.github.lzocateli.PixelPointer` |
| `pixelpointer grant` | GNOME/Wayland: pede a permissão de captura com uma janela focada |
| `pixelpointer selftest` | Confere as bibliotecas nativas sem tela (usado pelos testes de pacote) |
| `pixelpointer --version` / `--help` | Versão e ajuda |

Teclas na sobreposição: roda do mouse ou setas para o zoom, clique e arraste
para desenhar, R/G/B/Y/O/P para cores, Ctrl+roda para a espessura, Ctrl+Z para
desfazer, E para apagar, W/K para quadro branco/preto, Ctrl+S para salvar PNG
no diretório atual, Esc ou botão direito para sair.

## Permissões por sistema

### Linux com Wayland (GNOME, KDE, Hyprland)

O Wayland não permite que um programa veja a tela ou o teclado de outros; o
PixelPointer usa os portais XDG do desktop.

1. `pixelpointer install-desktop-file`: sem o `.desktop`, o desktop não associa
   permissões e atalhos ao PixelPointer. O pacote `.deb` já instala esse arquivo.
2. `pixelpointer grant` (GNOME): abre uma janela do PixelPointer; pressione
   Enter e clique em **Permitir**. O GNOME só mostra esse diálogo para o
   aplicativo focado, por isso ele não pode ser pedido a partir do terminal.
3. `pixelpointer daemon`: na primeira vez, o desktop mostra os atalhos propostos
   (Ctrl+1 e Ctrl+2) para você confirmar ou trocar. Nas próximas, o vínculo é
   imediato.

Se o desktop não oferece o portal GlobalShortcuts (Sway, GNOME anterior ao 48),
crie atalhos do próprio desktop que executem o comando:

| Desktop | Configuração |
| --- | --- |
| GNOME | Configurações → Teclado → Atalhos personalizados: comando `pixelpointer zoom`, atalho Ctrl+1 |
| KDE Plasma | Configurações do sistema → Atalhos → Adicionar comando: `pixelpointer zoom` |
| Sway | `bindsym Ctrl+1 exec pixelpointer zoom` em `~/.config/sway/config` |
| Hyprland | `bind = CTRL, 1, exec, pixelpointer zoom` em `hyprland.conf` |

Com o `daemon` em execução, `pixelpointer zoom` só envia o comando pelo socket
local (`$XDG_RUNTIME_DIR/pixelpointer.sock`) e termina em milissegundos.

### Linux com X11

Atalhos e captura funcionam sem permissões adicionais (XGrabKey e XGetImage).

### macOS

Na primeira captura, o macOS pede a permissão de **Gravação de Tela** em
Ajustes do Sistema → Privacidade e Segurança. A versão 1 não é assinada com
Developer ID: o macOS pode pedir a permissão de novo após cada atualização.
Os atalhos globais nativos chegam numa próxima versão; até lá, use
`pixelpointer zoom` num atalho do sistema (Atalhos/Automator) ou num utilitário
como Raycast.

### Windows

Não há permissões adicionais. Ctrl+1 e Ctrl+2 são registrados com
`RegisterHotKey`; se outro programa já usa a combinação, o `daemon` informa a
falha.

## Verificar os downloads

```bash
sha256sum --check --ignore-missing SHA256SUMS
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer
gh attestation verify pixelpointer-linux-x64.tar.gz --repo lzocateli/PixelPointer \
  --predicate-type https://spdx.dev/Document/v2.2
```

## Desinstalar

| Instalação | Remoção |
| --- | --- |
| `install.sh` | `rm -rf ~/.local/lib/pixelpointer ~/.local/bin/pixelpointer` (ou o `--prefix` usado) |
| `install.ps1` | apague `%LOCALAPPDATA%\Programs\pixelpointer` |
| `.deb` | `sudo apt remove pixelpointer` |
| Homebrew | `brew uninstall pixelpointer` |
| winget | `winget uninstall lzocateli.PixelPointer` |

No Linux, remova também `~/.local/share/applications/io.github.lzocateli.PixelPointer.desktop`
se tiver usado `install-desktop-file`. No GNOME, os atalhos vinculados podem ser
removidos nas Configurações; a permissão de captura, com:

```bash
gdbus call --session --dest org.freedesktop.impl.portal.PermissionStore \
  --object-path /org/freedesktop/impl/portal/PermissionStore \
  --method org.freedesktop.impl.portal.PermissionStore.DeletePermission \
  screenshot screenshot io.github.lzocateli.PixelPointer
```

## Solução de problemas

| Sintoma | Causa provável e solução |
| --- | --- |
| `erro: O portal recusou a solicitação (código 2)` | Permissão de captura ausente no GNOME: rode `pixelpointer grant` |
| `aviso: registro do app id falhou` | `.desktop` ausente: rode `pixelpointer install-desktop-file` |
| Atalhos indisponíveis no Wayland | Desktop sem o portal GlobalShortcuts: use um atalho do desktop (tabela acima) |
| A janela abre mas não recebe o teclado | Compositor sem activation token; clique na janela uma vez |
| `selftest falhou` no Linux | glibc anterior a 2.34 ou `libfontconfig` ausente: use Ubuntu 22.04, Debian 12, RHEL 9 ou mais novos e instale `libfontconfig1` |
| Atraso de ~0,5 s ao abrir no Wayland | Custo do portal Screenshot; a captura contínua por PipeWire está planejada para a F2 |

Para relatar um problema, inclua a saída de `pixelpointer probe` e siga o
[suporte](../.github/SUPPORT.md).

## Limitações atuais

- Timer (Ctrl+3), Live Zoom (Ctrl+4), gravação (Ctrl+5), recorte (Ctrl+6) e
  DemoType (Ctrl+7) ainda não existem; veja o [plano](plano-implementacao.md).
- Em várias telas no Wayland, o compositor escolhe o monitor da janela.
- macOS sem atalhos globais nativos e sem assinatura na versão 1.
