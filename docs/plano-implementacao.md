# Plano de implementação — PixelPointer

**Português (Brasil)** · [English](en/plano-implementacao.md) · [Español](es/plano-implementacao.md)

| Campo | Valor |
| --- | --- |
| Status inicial | aprovado para F0 (decisões de 2026-10-29) |
| Status do plano (2026-11-04) | **Ativo**. A seção 8 é a fonte única das pendências de implementação, validação, decisão e documentação |
| Estado de entrega (2026-11-04) | v0.3.1 é a última versão estável publicada. F0/F1 entregues; zoom, anotação, timer, recorte, Live Zoom e GIF estão implementados. O aceite GNOME/X11 de 2026-11-04 cobre manualmente as funcionalidades disponíveis no pacote local, mas não representa release publicada nem validação multiplataforma. Veja as [pendências consolidadas](#8-pendências-consolidadas) |
| Primeira versão funcional | v0.1.2, aceita como estável pelo mantenedor em 2026-10-30 no Ubuntu 24.04.5/GNOME X11 e Ubuntu 26.04/GNOME Wayland; aceite manual limitado aos fluxos e ambientes registrados, sem concluir F2/F3 ou validar Windows/macOS |
| Origem | IA assistida |
| Revisor humano | Lincoln Zocateli |
| Repositório | <https://github.com/lzocateli/PixelPointer> |
| Licença | MIT |

**Aceite complementar em 2026-10-30:** o solicitante confirmou desenho
transparente, zoom, exportação PNG com alfa, CapsLock/NumLock e uso nos dois
monitores 1920×1080 em escala 1 no Ubuntu 24.04.5/GNOME X11, autorizando considerar
esses testes concluídos. O [registro de evidências e limites](plano-e2e-grafico-uv.md#aceite-manual-do-desenho-transparente-em-gnome-x11)
separa esse aceite manual do E2E automatizado. Na data daquele aceite, F3 ainda
estava incompleta; formas, texto e blur foram implementados depois. As lacunas
atuais de F3 são multi-monitor Wayland e validação por plataforma/topologia,
conforme a seção 8. Prints complementares do solicitante, na mesma
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

O diagrama a seguir registra a arquitetura-alvo. Na implementação atual, os
projetos existentes são `PixelPointer.App`, `PixelPointer.Core` e
`PixelPointer.Rendering`; as abstrações e projetos `PixelPointer.Platform*`
listados abaixo ainda não foram extraídos do aplicativo.

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

**Estado F2 (2026-11-04):** `src/PixelPointer.Core` contém o modelo de
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
CPU é usada. No Wayland fora do GNOME, a captura usa ScreenCast + PipeWire sob
demanda (D13): 36 ms no Wayland isolado com sway, contra ~550 ms
do Screenshot no GNOME da F0. A preview teve aceite de captura, zoom e desenho no GNOME real
em 2026-10-31; o primeiro quadro levou 122 ms, mas o tempo do portal incluiu
consentimento humano. Esses números não são benchmarks equivalentes. Os recursos principais de
captura, configuração, modelo, renderização e ferramentas de anotação estão
implementados;
os critérios residuais de F2 e a decisão sobre as abstrações de plataforma estão
centralizados na [seção 8](#8-pendências-consolidadas).

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
| Captura estática | GDI | ScreenCaptureKit (permissão Gravação de Tela) | XGetImage/XShm | Screenshot no GNOME; ScreenCast/PipeWire nos demais compositores quando há um monitor; Screenshot para múltiplos monitores ou fallback |
| Sobreposição em tela cheia | SDL3 | SDL3 + nível de janela | SDL3 | SDL3 Wayland nativo (**validado**, escala fracionada ok) |
| Live Zoom | Implementado pela Magnification API | Implementado via ScreenCaptureKit | Implementado como lupa em janela | Best effort via ScreenCast e cursor metadata; um monitor, portal compatível e GNOME desabilitado por D13 |
| Gravação | GIF implementado | Não implementada | GIF implementado | GIF implementado via sessão ScreenCast |
| DemoType | Não implementado | Não implementado | Não implementado | Não implementado; portal RemoteDesktop/libei continua como possibilidade, não como suporte existente |

## 5. Fases

As fases abaixo resumem o estado; tarefas executáveis e critérios de validação
ficam exclusivamente na [seção 8](#8-pendências-consolidadas).

| Fase | Estado | Resumo |
| --- | --- | --- |
| **F0 — Spike** | Concluída | Relatório go/no-go ([f0-relatorio.md](f0-relatorio.md)) |
| F1 — Fundação | Concluída | Governança, CI, releases e documentação inicial entregues |
| F2 — Core + captura rápida | Parcial | Core, captura e configuração implementados; critérios residuais estão na seção 8 |
| F3 — Zoom + Desenho | Parcial | Ferramentas principais implementadas; cobertura Wayland/plataformas ainda incompleta |
| F4 — Timer + Recorte | Implementada | Aceite manual local GNOME/X11 em 2026-11-04 |
| F5 — Live Zoom | Parcial | Windows/macOS/X11 implementados; Wayland é best effort |
| F6 — Gravação | Parcial | GIF implementado em Windows e Linux X11/Wayland |
| F7 — DemoType | Não iniciada | Ainda sem implementação |
| F8 — Distribuição | Parcial | Canais atuais implementados; formatos adicionais aguardam decisão de escopo |
| F9 — i18n e documentação | Parcial | Documentação corrente pt-BR/en/es; faltam traduções de documentos legados |

## 6. Riscos

Os riscos ativos e suas próximas ações estão registrados na tabela única da
[seção 8](#8-pendências-consolidadas); este plano não mantém uma lista paralela.

## 7. Questões em aberto

Decisões de escopo e tarefas em aberto são acompanhadas somente na
[seção 8](#8-pendências-consolidadas).

## 8. Pendências consolidadas

Esta é a fonte única de status e próxima ação para pendências de implementação,
validação, decisão e documentação. Os planos de E2E e os registros de aceite
mantêm evidências e limites históricos, sem manter outro backlog. Atualizado em
2026-11-04; os históricos de PR estão no repositório e os resultados gráficos no
[plano E2E](plano-e2e-grafico-uv.md).

| ID | Tipo | Status | Pendência e critério de conclusão |
| --- | --- | --- | --- |
| IMPL-01 | Implementação | Parcial | Implementar captura/seleção de monitor no Wayland, preservando a política Screenshot do GNOME; validar a saída escolhida e o recorte em compositor real |
| IMPL-02 | Implementação | Pendente | Tornar assíncrono o início da gravação Wayland; consentimento, espera do portal e falhas não podem bloquear callbacks do daemon |
| IMPL-03 | Implementação | Não implementada | Definir e implementar gravação GIF no macOS, MP4 e áudio somente após selecionar componentes com licença permissiva verificada; FFmpeg não é opção aprovada |
| IMPL-04 | Implementação | Não iniciada | Projetar e implementar DemoType por plataforma, incluindo autorização/limites de injeção de entrada no Wayland |
| IMPL-05 | Implementação/arquitetura | Não extraída | Decidir se as abstrações `PixelPointer.Platform*` continuam necessárias; se sim, extraí-las de `PixelPointer.App` sem duplicar os adaptadores atuais |
| DEC-01 | Decisão de escopo | Pendente | Decidir se `.zip`, `.dmg`, AppImage e Flatpak seguem no escopo; não implementar início automático sem decisão explícita |
| DOC-01 | Documentação | Pendente | Criar traduções en/es do relatório F0 e do plano histórico da issue #3 |
| VAL-01 | Validação/desempenho | Meta não atingida | Medir e tratar a latência GNOME: captura observada em 520–531 ms e primeiro quadro em 585–635 ms, contra meta < 150 ms; registrar decisão se a meta mudar |
| VAL-02 | Validação/desempenho | Pendente | Medir renderer GPU em hardware real e 4K; decidir, com os resultados, se deixa de ser opt-in |
| VAL-03 | Validação nativa | Pendente | Definir execução compatível com as regras de containers (ou obter autorização explícita para exceção) e completar o roteiro Windows de captura, atalhos, GIF, Ctrl+S, tray e encerramento; registrar SO/arquitetura e evidências |
| VAL-04 | Validação nativa | Pendente | Definir execução compatível com as regras de containers (ou obter autorização explícita para exceção) e validar no macOS captura, Live Zoom, permissões, instalação e atalhos; gravação não entra como critério até ser implementada |
| VAL-05 | Compatibilidade/E2E | Parcial | Executar matriz Wayland em GNOME/KDE e outros compositores: portais naturalmente incompatíveis, consentimento real, atalhos físicos e tray; Sway isolado já cobre ScreenCast, gravação, ciclo de vida, recusa controlada de `GetAll` e escalas 1/1,5/2 |
| VAL-06 | Hardware/E2E | Parcial | Validar conflitos de atalho, caneta/pressão, múltiplos monitores, escalas e GPUs em hardware real; X11/Windows e a configuração GNOME/X11 de dois monitores têm evidência local, sem equivaler à matriz completa |
| VAL-07 | Compatibilidade | Pendente | Executar e registrar testes em Linux arm64 em hardware apropriado |

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
