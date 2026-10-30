# Relatório da F0 — spike de viabilidade

| Campo | Valor |
| --- | --- |
| Data | 2026-10-29 |
| Código | Spike promovido a [`src/PixelPointer.App`](../src/PixelPointer.App) em 2026-10-30 (binário `pixelpointer`) |
| Ambiente validado | Ubuntu 26.04.1 LTS, GNOME Shell 50.1, Wayland, xdg-desktop-portal 1.21.1, notebook 1920×1080 com escala 125% |
| Stack | .NET 10.0.13, SDL 3.5.0 (ppy.SDL3-CS 2026.1002.1), SkiaSharp 4.154.0, Tmds.DBus.Protocol 0.95.2 |
| Recomendação | **GO** com SDL3 + SkiaSharp, condicionada aos itens pendentes da seção 5 |

## 1. O que o spike faz

| Comando | Função |
| --- | --- |
| `probe` | SO, sessão, SDL, monitores, versões dos portais, registro do app id |
| `capture` | Captura a tela e mede a latência |
| `overlay [--image] [--mode zoom\|draw]` | Captura e abre a sobreposição |
| `daemon` | Registra Ctrl+1/Ctrl+2 e o socket IPC |
| `zoom` / `draw` | Envia o comando ao daemon por IPC (ou executa direto) |
| `install-desktop-file` / `grant` | Primeiro uso no GNOME: `.desktop` do app id e permissão de captura com janela focada |

A sobreposição implementa o essencial do zoom e do desenho: zoom animado seguindo o mouse, roda/setas, desenho com
mouse ou caneta (pressão), cores R/G/B/Y/O/P, espessura, Ctrl+Z, E, quadro branco/preto (W/K), Ctrl+S, botão
direito/Esc.

## 2. Resultados no GNOME 50 Wayland

| Item | Resultado |
| --- | --- |
| Build | Sem warnings com `TreatWarningsAsErrors` e analisadores AOT (`IsAotCompatible`) |
| Driver de vídeo SDL | `wayland` nativo, renderer `opengl` |
| Registro de app id (`org.freedesktop.host.portal.Registry`) | OK após instalar `io.github.lzocateli.PixelPointer.desktop` |
| Captura (portal Screenshot, não interativa) | OK, 1920×1080 físicos; **529–578 ms** (8 medições) |
| Atalhos globais (portal GlobalShortcuts v1) | OK: diálogo do GNOME na 1ª vez; nas seguintes o vínculo é imediato (`<Control>1`, `<Control>2`); sinal `Activated` traz `activation_token` |
| Foco da janela aberta pelo atalho | OK com o activation token (a janela recebe o teclado) |
| Sobreposição | Tela cheia 1920×1080 px, densidade 1,25 (escala fracionada correta) |
| Atalho → 1º quadro | **608–660 ms** (captura ~540 ms + janela ~80–110 ms) |
| Desenho (Skia CPU + upload da textura) | média 2,4 ms, p95 3,0–3,7 ms em 1080p sem zoom; 8,6 ms com zoom 2× |
| IPC por socket Unix | Ida e volta em 0,8 ms |
| Encerramento por SIGTERM/SIGINT | OK, com remoção do socket |
| Native AOT (linux-x64) | Publish sem warnings de trimming/AOT; executável 3,3 MB + `libSDL3.so` 4,2 MB + `libSkiaSharp.so` 11,7 MB = **19,2 MB** (sem o `.dbg`); depende só de libc/libm |
| Inicialização AOT | < 10 ms e ~5 MB de RSS (`help`); captura pelo binário AOT em 525–527 ms |
| Avaliação do mantenedor | "ficou perfeito; se conseguir reduzir o tempo ficará ainda melhor" |

## 3. Achados

1. **O GNOME só mostra diálogos de acesso para o app focado.** Pedir a permissão de captura a partir do terminal
   resultou em `AccessDenied: Only the focused app is allowed to show a system access dialog` (resposta 2 do
   portal). Com uma janela do PixelPointer focada (`grant`), o diálogo apareceu e a permissão ficou gravada no
   PermissionStore (`screenshot/screenshot: io.github.lzocateli.PixelPointer = yes`). Vira a decisão D9.
2. **Apps fora de sandbox precisam do `.desktop` para ter app id.** Sem ele, `Registry.Register` falha com
   `App info not found`, e permissões e atalhos não ficam associados ao PixelPointer.
3. **O SDL não pode rodar na continuação de um `await`.** O laço da janela ocupou a thread de leitura do D-Bus e
   nenhuma resposta dos portais chegava. Com `Main` síncrono e SDL na thread principal, tudo funciona; é também
   o requisito do macOS. Vira a decisão D8.
4. **`MessageWriter` do Tmds.DBus é um `ref struct` mutável.** Passado por valor a um `Action<MessageWriter>`,
   a mensagem saía vazia e nunca era enviada. Escrever sempre por `ref`.
5. **O SDL instala tratadores de SIGINT/SIGTERM** e o daemon deixava de encerrar. Corrigido com
   `SDL_HINT_NO_SIGNAL_HANDLERS` + `PosixSignalRegistration`. Vira a decisão D10.
6. **A latência vem quase toda do portal Screenshot**, que grava um PNG em disco a cada captura. O ScreenCast com
   PipeWire mantém o fluxo aberto e entrega o quadro atual sem esse custo; passa para a F2, junto com o Live Zoom.
7. **Desenho em CPU escala com a área de pixels.** Em 4K a estimativa é de ~35 ms por quadro com zoom. Skia sobre
   OpenGL entra na F2.
8. A captura do portal é gravada pelo GNOME na pasta de capturas do usuário; o spike apaga o arquivo depois de
   ler. Com o ScreenCast isso deixa de acontecer.

## 4. Implementado no spike, mas não exercitado aqui

| Plataforma | Atalhos | Captura |
| --- | --- | --- |
| Windows | `RegisterHotKey` numa thread com fila de mensagens | GDI `BitBlt` da área de trabalho virtual |
| macOS | não implementado (usar `pixelpointer zoom` por IPC) | `screencapture -x` (provisório; definitivo é ScreenCaptureKit) |
| Linux X11 | `XGrabKey` com variações de CapsLock/NumLock | `XGetImage` |

O GNOME 50 não tem mais sessão X11; o caminho X11 precisa de outra máquina ou VM (por exemplo, Xfce/MATE/i3).

## 5. Pendências para fechar a F0

| # | Pendência | Como |
| --- | --- | --- |
| ~~P1~~ | ~~Publish Native AOT local~~ | Concluído: ver seção 2 |
| P2 | Publish AOT em Windows e macOS | Jobs `validate-windows` e `validate-macos` do [`ci.yml`](../.github/workflows/ci.yml), que publicam o build como artefato |
| P3 | Teste manual em Windows e macOS | Roteiro da seção 6 com os artefatos do workflow |
| P4 | X11 e KDE Plasma Wayland | Mesmo roteiro numa VM |

## 6. Roteiro de teste manual (por plataforma)

Baixe o artefato `pixelpointer-<rid>` da execução mais recente do CI (aba Actions) ou instale
pela [release](https://github.com/lzocateli/PixelPointer/releases).


1. `pixelpointer probe` e anotar a saída.
2. Linux Wayland: `install-desktop-file` e `grant` (aceitar o diálogo).
3. `pixelpointer capture` três vezes e anotar as latências.
4. `pixelpointer daemon` e, em outra janela, pressionar Ctrl+1: zoom com a roda, clique para desenhar,
   R/G/B, Ctrl+Z, W, Esc. Repetir com Ctrl+2.
5. Com o daemon rodando, `pixelpointer zoom` em outro terminal (caminho por IPC).
6. macOS: conceder a permissão de Gravação de Tela ao terminal ou ao binário quando o sistema pedir.
7. Copiar o bloco `--- sessão ---` impresso pelo daemon para este relatório.
