# Plano de implementação — PixelPointer

**Português (Brasil)** · [English](en/plano-implementacao.md) · [Español](es/plano-implementacao.md)

| Campo | Valor |
| --- | --- |
| Status inicial | aprovado para F0 (decisões de 2026-10-29) |
| Estado de entrega | F0 validada no Linux/GNOME Wayland, inclusive Native AOT; Windows e macOS pendentes do roteiro manual ([relatório](f0-relatorio.md)). F1 (fundação: repositório, governança, CI/CD e distribuição) entregue em 2026-10-30 |
| Primeira versão funcional | v0.1.2, aceita como estável pelo mantenedor em 2026-10-30 no Ubuntu 24.04.5/GNOME X11 e Ubuntu 26.04/GNOME Wayland; aceite manual limitado aos fluxos e ambientes registrados, sem concluir F2/F3 ou validar Windows/macOS |
| Origem | IA assistida |
| Revisor humano | Lincoln Zocateli |
| Repositório | <https://github.com/lzocateli/PixelPointer> |
| Licença | MIT |

**Aceite complementar em 2026-10-30:** o solicitante confirmou desenho
transparente, zoom, exportação PNG com alfa, CapsLock/NumLock e uso nos dois
monitores 1920×1080 em escala 1 no Ubuntu 24.04.5/GNOME X11, autorizando considerar
esses testes concluídos. O [registro de evidências e limites](plano-e2e-grafico-uv.md#aceite-manual-do-desenho-transparente-em-gnome-x11)
separa esse aceite manual do E2E automatizado. Não conclui a fase F3 inteira:
formas, texto, blur, HiDPI e paridade entre plataformas continuam no escopo futuro,
assim como o aceite real de Zoom/Desenhar pela tray, do launcher e a
revisão dos vídeos automatizados. Prints complementares do solicitante, na mesma
data, registram anotações no monitor 1 e comprovam o ícone da tray GNOME visível
com o menu Zoom/Desenhar/Sair aberto; esse recorte visual está verificado.
O solicitante também confirmou o encerramento por Sair e forneceu print com
retorno ao prompt e `status` informando ausência do daemon; esse fluxo está
concluído por aceite manual no mesmo GNOME/X11, sem comprovar abertura sem terminal.

**Aceite manual Ubuntu 26.04/GNOME Wayland em 2026-10-30:** após a instalação
da release `v0.1.2`, o solicitante informou “tudo funcionando” e anexou seis
prints nesta conversa. O aceite cobre os fluxos exercitados nessa sessão,
conforme relato humano. Os prints mostram anotações sobre a imagem da área de
trabalho, quadros branco e preto com os mesmos traços, retorno ao fundo da
captura, ícone próprio no launcher e menu Zoom/Desenhar/Sair na tray.
O [registro detalhado](plano-e2e-grafico-uv.md#aceite-manual-no-ubuntu-2604gnome-wayland)
separa observação visual de relato: imagens estáticas não comprovam fundo ao vivo,
alfa do PNG, acionamento pelo launcher/tray, latência ou o backend de captura.
Esse resultado não conclui F2/F3, nem estende o aceite a outros compositores,
monitores, escalas, Windows ou macOS. A cobertura E2E automatizada Wayland
continua pendente. Os anexos permanecem na conversa, sem cópia para o repositório.

**Evidência complementar de transparência, na mesma data:** o solicitante anexou
mais dois prints e os identificou como evidências de desenho com transparência.
Ambos mostram traços vermelhos sobre um vídeo visível no navegador, em conteúdos
diferentes. O desenho transparente fica aceito manualmente nessa sessão
Ubuntu 26.04/GNOME Wayland, com relato humano e evidência visual da sobreposição.
Os prints não demonstram reprodução contínua sob um mesmo traço nem alfa do PNG;
essas verificações objetivas continuam distintas do aceite manual.

## 1. Objetivo

O PixelPointer é uma ferramenta de apresentação Native AOT (`pixelpointer`) para Linux, Windows e macOS, com as
funcionalidades do ZoomIt (Sysinternals/PowerToys): zoom de tela, anotação, timer de intervalo, Live Zoom,
recorte, gravação e DemoType, acionadas por atalhos globais.

Não é objetivo reproduzir a marca, os ícones ou a interface do ZoomIt. O código do ZoomIt no PowerToys é MIT e
pode servir de referência de comportamento, com atribuição quando algo for adaptado.

## 2. Decisões registradas

| # | Decisão | Valor |
| --- | --- | --- |
| D1 | Nome do produto e do binário | PixelPointer / `pixelpointer`; app id `io.github.lzocateli.PixelPointer` |
| D2 | Licença | MIT |
| D3 | Stack | .NET 10 Native AOT; SDL3 (ppy.SDL3-CS) para janela e entrada; SkiaSharp para desenho; Tmds.DBus.Protocol para portais XDG. Avalonia descartado: no Linux roda via XWayland e o produto é quase todo uma janela de sobreposição |
| D4 | Wayland | X11 nativo → portal GlobalShortcuts → `pixelpointer hotkeys install` (atalho do desktop chamando a CLI por IPC). Live Zoom e DemoType em best effort |
| D5 | macOS | Primeira versão não assinada; assinatura e notarização com Developer ID quando disponível |
| D6 | MP4 no Linux | `ffmpeg` do sistema quando existir (não embarcado); GIF nativo em C# nas três plataformas |
| D7 | Local | `~/projetos/PixelPointer`, processo e governança como no DotnetScope |
| D8 | Threading (da F0) | `Main` síncrono; SDL sempre na thread principal; trabalho assíncrono (D-Bus, IPC) em outras threads, entregue à thread principal por fila. Um laço de janela na continuação de um `await` bloqueia a thread de leitura do D-Bus |
| D9 | Primeiro uso no GNOME (da F0) | A permissão de captura e o vínculo de atalhos exigem uma janela do PixelPointer focada e o `.desktop` instalado com o app id; o instalador e a janela de boas-vindas cuidam disso |
| D10 | Sinais (da F0) | `SDL_HINT_NO_SIGNAL_HANDLERS=1`; o app trata SIGINT/SIGTERM com `PosixSignalRegistration` |
| D11 | Idiomas da documentação (2026-10-30) | pt-BR é a referência (`README.md`, `docs/*.md`); traduções em `README.en.md`, `README.es.md`, `docs/en/` e `docs/es/`. A regra vigente em `.github/instructions/documentation.instructions.md` inclui também planos e relatórios, substituindo o escopo anterior restrito às jornadas de entrada e uso |
| D12 | Pacotes e plataformas (2026-10-30) | Pacotes levam executável, SDL3, libSkiaSharp, ícone SVG, `LICENSE` e `THIRD-PARTY-NOTICES.md`; instaladores criam diretório do aplicativo e link do comando. Mínimo Linux glibc 2.34 (exigência da libSDL3 distribuída) e sem `linux-musl` (SDL3-CS sem binário musl) |

## 3. Arquitetura

```text
src/
  PixelPointer.Core/              modelo de anotações, ferramentas, undo/redo, configurações (JSON source-gen), comandos
  PixelPointer.Rendering/         Skia: traços, formas, texto, blur, zoom/pan, timer; CPU e GPU (GL)
  PixelPointer.Platform/          interfaces: IHotkeyService, IScreenCapture, ILiveCapture, IClipboard, IInputInjector, IRecorder
  PixelPointer.Platform.Windows/  Win32 RegisterHotKey, DXGI/Windows.Graphics.Capture, Media Foundation, SendInput
  PixelPointer.Platform.MacOS/    Carbon RegisterEventHotKey, ScreenCaptureKit, AVFoundation, CGEventPost
  PixelPointer.Platform.Linux/    X11 (XGrabKey/XShm/XTest) e portais (GlobalShortcuts/Screenshot/ScreenCast/RemoteDesktop) + PipeWire
  PixelPointer.App/               executável AOT: tray, sobreposição, configurações, CLI/IPC (hoje concentra o código promovido do spike)
tests/
  PixelPointer.UnitTests/         xunit.v3 + Microsoft.Testing.Platform
  PixelPointer.IntegrationTests/  renderização determinística comparada com imagens de referência
```

O mesmo binário é app e CLI: `pixelpointer zoom|draw|timer <min>|snip` envia o comando por socket Unix
(`$XDG_RUNTIME_DIR` ou diretório temporário; AF_UNIX também no Windows 10+) à instância em execução.

## 4. Matriz de capacidades por plataforma

| Capacidade | Windows | macOS | Linux X11 | Linux Wayland |
| --- | --- | --- | --- | --- |
| Atalho global | RegisterHotKey | RegisterEventHotKey | XGrabKey | Portal GlobalShortcuts (**validado no GNOME 50**); fallback atalho do desktop + IPC |
| Captura estática | DXGI/WGC | ScreenCaptureKit (permissão Gravação de Tela) | XShm | Portal Screenshot (**validado**, ~550 ms) → ScreenCast/PipeWire contínuo |
| Sobreposição em tela cheia | SDL3 | SDL3 + nível de janela | SDL3 | SDL3 Wayland nativo (**validado**, escala fracionada ok) |
| Live Zoom | Magnification API ou WGC | ScreenCaptureKit excluindo a própria janela | XComposite (trabalhoso) | ScreenCast + layer-shell onde houver; GNOME best effort |
| Gravação | Media Foundation | AVFoundation | ffmpeg do sistema / GIF | ScreenCast + ffmpeg / GIF |
| DemoType | SendInput | CGEventPost (Acessibilidade) | XTest | Portal RemoteDesktop/libei, best effort |

## 5. Fases

| Fase | Entrega | Critério de saída |
| --- | --- | --- |
| **F0 — Spike** | SDL3+Skia+AOT, captura, atalhos, IPC por plataforma | Relatório com go/no-go ([f0-relatorio.md](f0-relatorio.md)) |
| F1 — Fundação | Repositório público, Altivy.Groundwork (governança, release-please, gitleaks, política), CI matriz, publish dos 6 RIDs com SBOM e atestação, `.deb`, instaladores, Homebrew e winget, documentação pt/en/es; o spike vira `src/PixelPointer.App` | CI verde nos 3 SOs |
| F2 — Core + captura rápida | Modelo de anotações, renderização Skia em **GPU**, configurações, abstrações de plataforma, IPC/CLI, **ScreenCast/PipeWire no Wayland** (meta: atalho → 1º quadro < 150 ms), ScreenCaptureKit no macOS, janela de boas-vindas (D9) | Testes unitários e de imagem; latência medida |
| F3 — Zoom + Desenho | Ctrl+1/Ctrl+2 completos: formas (linha, seta, retângulo, elipse), marca-texto, blur, texto, multi-monitor, HiDPI, copiar/salvar | Paridade funcional validada manualmente |
| F4 — Timer + Recorte | Ctrl+3 e Ctrl+6 (área de transferência e arquivo) | — |
| F5 — Live Zoom | Windows → macOS → X11; Wayland best effort | Matriz documentada |
| F6 — Gravação | GIF nativo; MP4 por plataforma; ffmpeg opcional no Linux; áudio | — |
| F7 — DemoType | Windows, macOS, X11; Wayland pelo portal | — |
| F8 — Distribuição | winget/zip, `.dmg` (não assinado na v1) + Homebrew cask, `.deb`/AppImage/Flatpak, início automático, `.desktop` | Instalação limpa nos 3 SOs |
| F9 — i18n e documentação | pt-BR e en, guia de permissões por SO | — |

## 6. Riscos

| Risco | Mitigação |
| --- | --- |
| Latência da captura no Wayland (~550 ms pelo portal Screenshot) | ScreenCast com PipeWire e restore token na F2 |
| Desenho em CPU insuficiente em 4K | Skia sobre OpenGL (GRContext) na F2; CPU fica como fallback |
| GNOME exige janela focada para diálogos de permissão | Janela de boas-vindas (D9) |
| macOS sem assinatura: permissões TCC se perdem entre versões | Documentar; assinar quando houver Developer ID (D5) |
| KDE/Hyprland/Sway com comportamentos diferentes de portal | Testar na F2; fallback por IPC sempre disponível |
| Pacotes nativos (SDL3, libSkiaSharp) ao lado do executável | Empacotar junto; avaliar link estático na F8 |

## 7. Questões em aberto

1. Multi-monitor no Wayland: a captura cobre a área de trabalho inteira e o compositor decide em que monitor a
   janela em tela cheia abre; escolher o monitor do cursor exige o ScreenCast por monitor (F2).
2. Tamanho do AOT em Windows e macOS (no Linux: 19,2 MB com SDL3 e Skia; inicialização < 10 ms).
3. Comportamento de Windows e macOS (aguarda o roteiro manual da seção 6 do relatório).
