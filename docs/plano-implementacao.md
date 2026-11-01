# Plano de implementação — PixelPointer

**Português (Brasil)** · [English](en/plano-implementacao.md) · [Español](es/plano-implementacao.md)

| Campo | Valor |
| --- | --- |
| Status inicial | aprovado para F0 (decisões de 2026-10-29) |
| Estado de entrega | F0 validada no Linux/GNOME Wayland, inclusive Native AOT; Windows com evidência manual parcial em 2026-10-31 e macOS pendente do roteiro manual ([relatório](f0-relatorio.md), [pendências](#8-pendências-consolidadas)). F1 (fundação: repositório, governança, CI/CD e distribuição) entregue em 2026-10-30 |
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

**Aceite da preview em 2026-10-31:** o solicitante testou
`0.1.3-preview.14` no Ubuntu 26.04.1 LTS x64/GNOME Wayland, anexou print
do consentimento ScreenCast e log de captura via PipeWire e confirmou
explicitamente zoom e desenho como funcionando. Captura, zoom e desenho
ficam aceitos manualmente nessa máquina. O PNG foi gerado em 1920×1080;
o portal levou 20.830 ms, incluindo a interação com o diálogo, o primeiro
quadro 122 ms e o total 20.999 ms. Isso não comprova a meta de atalho até
primeiro quadro < 150 ms, reutilização do restore token, duração do indicador,
atalhos físicos, tray, launcher, Ctrl+S ou transparência ao vivo. Esses
recortes, outros monitores/desktop e Windows/macOS não recebem aceite por
extensão. [Evidências e limites](plano-e2e-grafico-uv.md#aceite-manual-da-preview-no-gnome-wayland).

## 1. Objetivo

**Diretriz de evolução do mantenedor (2026-10-31):** próximas implementações
devem preservar a experiência e a qualidade já aceitas, limitando-se a melhorias
e correções legítimas. Regressões conhecidas bloqueiam integração; mudanças
experimentais não viram padrão sem evidência. Cada incremento deve validar seu
recorte e os comportamentos afetados, registrando também o que não foi testado.

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
| D13 | ScreenCast no Wayland (2026-10-31, decisão do mantenedor) | Sessão **sob demanda** por captura, fechada após o primeiro quadro, para o indicador de compartilhamento não ficar permanente; manter a sessão aberta só será avaliado com medição. `libpipewire-0.3` (MIT) do sistema via `LibraryImport`, sem redistribuir. Restore token em `$XDG_STATE_HOME/pixelpointer` (0600, nunca em logs). Somente com um monitor; caso contrário, ou em falha, portal Screenshot. Cancelamento do usuário não é contornado. **Revisão 2026-11-01 (decisão do mantenedor, opção A):** no GNOME a captura usa sempre o portal Screenshot, porque no GNOME 46 o indicador de compartilhamento ficava preso após ScreenCasts curtos; demais compositores mantêm ScreenCast |

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

**F2 em andamento (2026-10-31):** `src/PixelPointer.Core` já contém o modelo de
anotações (`AnnotationDocument`: traços, cor, espessura, quadro, desfazer e limpar),
sem SDL nem Skia, coberto por testes unitários. A sobreposição usa esse modelo sem
mudar atalhos nem aparência. Configurações somente leitura em `settings.json`
(cor e espessura iniciais, zoom inicial; JSON com source generator) estão
descritas no [guia de uso](uso.md#configurações). `src/PixelPointer.Rendering`
(`SceneRenderer`) desenha fundo, quadro e traços independentemente da janela,
com testes de imagem em CPU; a sobreposição mantém somente cursor e mensagens.
Renderização em GPU (Skia sobre OpenGL do SDL) existe como opt-in experimental
(`rendering.gpu`), com retorno automático à CPU. Em container com Xvfb e Mesa
llvmpipe (GLX), o zoom em OpenGL produziu pixels idênticos aos da CPU; a interface
OpenGL nativa do Skia pode não estar disponível com EGL (Wayland), caso em que a
CPU é usada. Falta aceite manual em hardware real e medição em 4K antes de
torná-la padrão. No Wayland com um monitor, a captura usa ScreenCast + PipeWire sob
demanda (D13): 36 ms no Wayland isolado com sway, contra ~550 ms do Screenshot no
GNOME da F0. A preview teve aceite de captura, zoom e desenho no GNOME real
em 2026-10-31; o primeiro quadro levou 122 ms, mas o tempo do portal incluiu
consentimento humano. Falta medir a latência de atalho até quadro sem esse
diálogo; os números não são um benchmark equivalente. Formas, texto, atalhos
configuráveis e os demais módulos acima continuam pendentes.

**Incremento de diagnóstico F2 (2026-10-31):** a captura agora informa tempos de
criação da sessão, seleção, início, persistência da autorização, abertura do
remoto PipeWire e leitura/conversão. Preserva os totais legados `portal` e
`primeiro quadro`; este último inclui abertura do remoto, não apenas leitura.
Informa somente se houve pedido de restauração, nunca o token. O fluxo de
consentimento, timeout, fechamento e fallback permanece igual. Testes
determinísticos cobrem a formatação; estes tempos não incluem todos os passos
de atalho até apresentação. [Interpretação](uso.md) e benchmark no GNOME real
continuam distintos; a F2 não fica encerrada por esse diagnóstico.

**Consulta agrupada de capacidades (F2, 2026-10-31):** versão, tipos de fonte e
modos do cursor são lidos por `Properties.GetAll`, reduzindo três leituras
individuais a uma quando a resposta é compatível. Não há cache de valores.
Resposta ausente, incompleta ou não suportada mantém o caminho `Get` anterior
nessa conexão, com a mesma ordem e saídas antecipadas; a sondagem inicial
incompatível custa uma chamada extra. A linha `ScreenCast capacidades` informa
o tempo e o método usados. Testes cobrem tipos, propriedades ausentes, suporte
de monitor, leituras legadas e cancelamento. Consentimento e fluxo de captura
permanecem iguais; benchmark e matriz de compatibilidade continuam pendentes.

**Compatibilidade sob recusa controlada (2026-11-01):** o orquestrador
`Test-WaylandScreenCast.ps1` ganhou `-DenyScreenCastGetAll`, que injeta uma
política obrigatória apenas no D-Bus descartável. O cenário padrão passou com
`GetAll`; o cenário de recusa confirmou duas rejeições no barramento, leitura
`Get`, duas capturas via portal real ScreenCast/PipeWire e quadro atualizado.
Não houve mudança no código de produção, nos portais ou no consentimento.
O teste não significa que o portal rejeite naturalmente `GetAll`: é injeção
de falha auditada. GNOME/KDE e respostas naturalmente incompatíveis continuam
pendentes. A repetição na mesma conexão foi validada no cenário isolado abaixo.
[Evidências e comando](plano-e2e-grafico-uv.md#compatibilidade-getall-sob-recusa-controlada).

**Repetição no daemon (2026-11-01):** `-RepeatDaemonCaptures` iniciou um único
daemon e exercitou desenho e zoom por IPC. O monitor D-Bus, restrito a chamadas
de propriedades, confirmou capacidades atualizadas: duas `GetAll` no controle
ou uma sondagem recusada seguida de seis `Get` ordenadas, sem nova tentativa de
`GetAll` na segunda sessão. Cores do quadro mudaram, o daemon continuou ativo
após fechar cada janela pelo compositor e `quit` encerrou normalmente.
Não houve alteração do código de produção nesta etapa. Não comprova atalhos
físicos, Esc, tray, quit com sobreposição aberta ou a matriz GNOME/KDE.
[Registro](plano-e2e-grafico-uv.md#capturas-repetidas-na-conexão-do-daemon).

O mesmo binário é app e CLI: `pixelpointer zoom|draw|timer <min>|snip` envia o comando por socket Unix
(`$XDG_RUNTIME_DIR` ou diretório temporário; AF_UNIX também no Windows 10+) à instância em execução.

## 4. Matriz de capacidades por plataforma

| Capacidade | Windows | macOS | Linux X11 | Linux Wayland |
| --- | --- | --- | --- | --- |
| Atalho global | RegisterHotKey | RegisterEventHotKey | XGrabKey | Portal GlobalShortcuts (**validado no GNOME 50**); fallback atalho do desktop + IPC |
| Captura estática | DXGI/WGC | ScreenCaptureKit (permissão Gravação de Tela) | XShm | ScreenCast/PipeWire sob demanda com um monitor (aceite manual da preview); fallback Screenshot |
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

## 8. Pendências consolidadas

**Sequência de execução solicitada em 2026-11-01:** cada etapa deve ter branch
própria, testes antes do commit, push e PR; nenhuma aprovação humana ou
validação nativa é substituída por resultados de container. A primeira entrega
consolida os incrementos locais na branch `perf/f2-wayland-capture`.

| Etapa | Estado de execução |
| --- | --- |
| 1. Consolidar diagnóstico, capacidades e testes Wayland | [PR #21](https://github.com/lzocateli/PixelPointer/pull/21) integrado em `df252e6`, com `Result` aprovado |
| 2. Nova preview e aceite GNOME real | [Preview #16](https://github.com/lzocateli/PixelPointer/actions/runs/41308984865) publicada e aceita manualmente pelo mantenedor em 2026-11-01 numa segunda máquina GNOME Wayland (1920×1080, escala 1,25): `install-desktop-file`, `grant`, daemon, Ctrl+1/Ctrl+2 pelo GlobalShortcuts, zoom, desenho, quadros branco/preto, PNG, menu da tray e Sair em duas execuções. Correções locais posteriores, ainda fora de preview: socket IPC órfão, entrada desktop de outra extração e aviso de ícone repetido |
| 3. Ciclo de vida completo e interação física | [PR #22](https://github.com/lzocateli/PixelPointer/pull/22) integrado em `0ad417f`, com `Result` aprovado; atalhos físicos, Esc, tray e launcher continuam pendentes |
| 4. Matriz de compatibilidade | [PR #23](https://github.com/lzocateli/PixelPointer/pull/23) integrado em `05b0c40`. Branch `test/wayland-output-scale`: correção de janela e escalas 1/1,5/2 passaram no cenário isolado; [PR #24](https://github.com/lzocateli/PixelPointer/pull/24) aberto. Ambientes reais, hardware e arquiteturas pendentes |
| 5. Fechamento F2 | [PR #25](https://github.com/lzocateli/PixelPointer/pull/25) integrado. Recorte `feat/first-use-setup`: 178 testes/AOT e fechamento gráfico sem efeitos passaram; [PR #26](https://github.com/lzocateli/PixelPointer/pull/26) aberto. Gerar nova preview após integração/CI verde para aceite manual. Consentimento GNOME real, GPU/4K, abstrações restantes e ScreenCaptureKit pendentes |
| 6. Ferramentas avançadas F3 | Branch `feat/f3-shapes`: linha, seta, retângulo e elipse com modificadores do ZoomIt (Shift, Ctrl+Shift, Ctrl, Tab), no modelo do Core, no renderer e na sobreposição, com testes de modelo e de imagem; aceite manual pendente. Marca-texto, blur, texto, multi-monitor e copiar continuam pendentes |
| 7. F4–F7 | Timer, recorte, Live Zoom, gravação e DemoType ainda não implementados |
| 8. F8–F9 | Distribuição adicional e traduções legadas ainda pendentes |

Atualizado em 2026-11-01. O [plano da issue #3](plano-correcao-issue-3.md) foi
encerrado com a issue e permanece como registro histórico. Os itens abertos dele
e do [plano de E2E](plano-e2e-grafico-uv.md) são acompanhados aqui, além das
fases F2 a F9 da seção 5.

| Pendência | Origem | Situação |
| --- | --- | --- |
| Aceite manual de Zoom e Desenhar acionados pela tray no GNOME X11 | Issue #3, 3.5 | Ícone, menu e Sair aceitos; as duas ações ainda sem confirmação no X11. No GNOME Wayland, Zoom e Desenhar pela tray foram aceitos com a preview.16 em 2026-11-01 |
| Abertura pelo menu de aplicativos GNOME sem terminal | Issue #3, 3.6 | Aceite real no GNOME Wayland com a preview.16 em 2026-11-01: o ícone em Programas iniciou o daemon com tray; GNOME X11 sem confirmação |
| Indicador de compartilhamento de tela permanece ativo no GNOME Wayland | Aceite da preview.16 | Mitigado localmente pela opção A da D13 (Screenshot no GNOME), sem sessões Mutter ScreenCast remanescentes no diagnóstico (GNOME Shell 46.0, mutter 46.2, xdg-desktop-portal-gnome 46.2). Falta aceite manual da nova preview e medir a latência do Screenshot no GNOME |
| Matriz Wayland por compositor: portais com e sem Registry, recusa de permissões, tray e E2E isolado com libei/EIS | Issue #3, 3.1, 3.3 e 5; E2E, 8 | ScreenCast/PipeWire passou em Sway isolado e captura, zoom e desenho tiveram aceite manual nas previews v0.1.3-preview.14 e v0.1.3-preview.16 no GNOME Wayland. Na preview.16, sessões repetidas reutilizaram o token sem diálogo (portal ~20 ms; 1º quadro ~160–176 ms após o comando). Duração do indicador, multi-monitor e outros compositores continuam pendentes |
| Ctrl+S nativo: Known Folder no Windows, inclusive OneDrive, e preferência do macOS, com filesystem real por SO | Issue #3, 3.7 | Encerrado por decisão do mantenedor em 2026-10-31; lógica coberta por testes, sem validação nativa registrada |
| Roteiro manual completo no Windows | F0; issue #3 | Evidência parcial em 2026-10-31, descrita abaixo |
| Roteiro manual completo no macOS | F0; issue #3 | Sem evidência nativa |
| Revisão humana dos vídeos e referências do E2E X11 | E2E, 6 e 8 | Encerrado por decisão do mantenedor em 2026-10-31, sem revisão registrada |
| Conflito físico de atalhos com outro aplicativo, escala fracionária, outras topologias e GPUs, caneta | Issue #3, 3.2 e 3.3; E2E, 8 | Pendente |
| Linux arm64 em arquitetura apropriada | E2E, 8 | Pendente |
| Completar traduções dos documentos autorais legados | F9; regra de documentação multilíngue | [Relatório F0](f0-relatorio.md) e plano histórico da issue #3 ainda sem versões en/es; README e guias atuais já traduzidos não encerram essa cobertura |

**Evidência parcial no Windows em 2026-10-31:** o solicitante anexou quatro
prints nesta conversa. Eles mostram a instalação por `install.ps1` com conferência
do SHA-256, o daemon registrando Ctrl+1 e Ctrl+2, o menu da tray com Zoom,
Desenhar e Sair e anotações sobre a tela. Também mostram dois problemas de
orientação, já documentados no [README](../README.md#instalação): `PATH`
desatualizado na sessão atual do PowerShell e `profile.ps1` bloqueado pela
política de execução `Restricted`. Os prints não identificam versão do Windows
nem arquitetura e não comprovam zoom, Ctrl+S com Known Folder, encerramento por
Sair ou execução em segundo plano. Os anexos permanecem na conversa, sem cópia
para o repositório.
