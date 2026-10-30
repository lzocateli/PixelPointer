# Plano de correção da issue #3

| Campo | Valor |
| --- | --- |
| Referência | [Issue #3: erro no Registry e integração ao desktop Linux](https://github.com/lzocateli/PixelPointer/issues/3) |
| Data | 2026-10-30 |
| Estado | Implementação e E2E linux-x64 concluídos; aceite manual GNOME/X11 de desenho, zoom, PNG, CapsLock/NumLock, uso nos monitores testados e encerramento por Sair concluído; ícone e menu da tray visíveis confirmados; Zoom/Desenhar pela tray, launcher real, portais e validações nativas Windows/macOS pendentes |
| Origem | Análise estática assistida por IA e alinhamento com o solicitante nesta conversa |
| Escopo inicial | Linux, com reprodução prioritária no Ubuntu/GNOME X11 relatado na issue |

Este documento registra o escopo, o estado de execução e as evidências da correção,
sem substituir o [README](../README.md) nem o
[plano de implementação](plano-implementacao.md). As limitações de validação gráfica
e de integração com portais permanecem explícitas abaixo.

## 1. Requisitos confirmados

- O aplicativo deve permanecer em segundo plano, sem depender de terminal aberto,
  mantendo Ctrl+1 para zoom e Ctrl+2 para desenho disponíveis quando o desktop
  permitir o registro desses atalhos.
- Esc ou o fechamento da sobreposição devem encerrar somente a sessão visual,
  preservando o daemon e os atalhos.
- O usuário deve poder clicar no ícone da tray e selecionar **Sair** para encerrar
  completamente o aplicativo e liberar seus recursos.
- Na ausência de suporte à tray, deve existir alternativa explícita de encerramento
  pelo comando proposto `pixelpointer quit`.
- X11 e Wayland devem usar classes específicas para comportamentos de plataforma,
  compartilhando a lógica comum. A preferência é um executável com seleção em tempo
  de execução. Compilações distintas ficam condicionadas a incompatibilidade técnica
  comprovada; não são necessárias com base na análise atual.
- Início automático na sessão do usuário é opcional e não deve ser habilitado
  silenciosamente. Execução em segundo plano não implica início automático.

## 2. Diagnóstico inicial

Registro histórico anterior às correções; o estado atual e as evidências estão
nas seções 3, 5 e 6.

| Ponto da issue | Evidência no código | Estado da conclusão |
| --- | --- | --- |
| Aviso sobre Registry | `TryRegisterAppIdAsync` retorna erros D-Bus como texto; o chamador recomenda instalar o desktop file sem distinguir interface ausente | Tratamento inadequado confirmado por leitura; compatibilidade requer teste |
| Ctrl+1 não funciona | O leitor de eventos X11 usava deslocamento fixo `84` para identificar a tecla | `84` corresponde à ABI de 64 bits; foi substituído por layout nativo, mas isso não comprova a causa do sintoma no Ubuntu relatado |
| Fundo preto/branco no desenho | A sobreposição desenha uma captura estática; W/K alternam quadros opcionais | Transparência real não implementada; causa do fundo incorreto ainda não reproduzida |
| Tray ausente | Não há implementação de tray no fluxo atual | Funcionalidade ausente |
| Ícone próprio ausente | A entrada desktop usa `Icon=zoom-in` | Identidade própria ausente |
| Segundo plano e saída pouco claros | O daemon informa atalhos, mas não oferece ciclo de vida por tray | Fluxo incompleto |

Módulos principais: [Program.cs](../src/PixelPointer.App/Program.cs),
[PortalClient.cs](../src/PixelPointer.App/Linux/PortalClient.cs),
[X11Hotkeys.cs](../src/PixelPointer.App/Linux/X11Hotkeys.cs),
[X11ScreenCapture.cs](../src/PixelPointer.App/Linux/X11ScreenCapture.cs),
[OverlaySession.cs](../src/PixelPointer.App/Overlay/OverlaySession.cs) e
[IpcChannel.cs](../src/PixelPointer.App/IpcChannel.cs).

## 3. Etapas de execução

### 3.1. Compatibilidade dos portais

**Prioridade: alta.**

- [x] Distinguir registro bem-sucedido, Registry indisponível e erro real,
      preservando o registro como primeira chamada de portal da conexão.
- [x] Tratar `UnknownMethod` e `UnknownInterface` no contexto do Registry como
      indisponibilidade, sem recomendar reinstalação indevida do desktop file.
- [x] No X11, dispensar a solicitação de permissão via `grant` para captura nativa
      e informar o motivo ao usuário.
- [x] No Wayland, manter permissões e alternativas conforme as capacidades do
      portal, sem esconder recusas ou falhas de autorização.

**Validação que pode refutar o diagnóstico:** executar com um serviço D-Bus que
oferece Registry e outro que não o oferece, diferenciando ausência de interface de
erro de registro. Reproduzir também a sequência descrita na issue.

**Estado da validação:** classificação coberta por testes unitários. No reteste
manual Ubuntu/GNOME X11, Registry indisponível foi informado como limitação e
não impediu captura, desenho ou zoom nativos. Integração com serviços que oferecem
Registry e a matriz de erros/permissões Wayland permanecem pendentes.

**Aceite:** ausência de aviso enganoso no ambiente sem Registry; captura X11
independente do portal; erros reais e limitações Wayland continuam identificáveis.

### 3.2. Atalhos Ctrl+1 e Ctrl+2 no X11

**Prioridade: alta.**

- [x] Comparar tamanho e deslocamentos de `XKeyEvent` com `sizeof`/`offsetof` da
      ABI nativa e substituir o deslocamento fixo por representação interoperável
      correta nas arquiteturas Linux suportadas.
- [x] Detectar conflitos de `XGrabKey` e não anunciar como disponível uma
      combinação que não foi registrada.
- [x] Encerrar a thread de eventos e liberar conexão e atalhos sem bloqueio.
- [x] Adicionar regressões para layout ABI e verificar modificadores no E2E X11.
- [x] Concluir o aceite manual de CapsLock/NumLock no GNOME X11, conforme
      confirmação e autorização explícita do solicitante em 2026-10-30.

**Validação que pode refutar o diagnóstico:** comparar o código da tecla recebido
com a definição nativa e comparar o atalho físico com `pixelpointer zoom`. Se o
comando direto também falhar, investigar separadamente captura/renderização.

**Aceite:** zoom e desenho abrem com CapsLock e NumLock ligados e desligados;
conflitos são comunicados; encerrar e reiniciar não deixa grabs residuais.

**Estado da validação:** layout ABI coberto por regressões; E2E com servidor X11
exercitou foco externo, as quatro combinações de CapsLock/NumLock, conflito real
de grab e reinício. O solicitante confirmou os testes com CapsLock/NumLock na
sessão GNOME X11 e autorizou considerá-los concluídos. Conflito físico com outro
aplicativo no GNOME não foi confirmado por esse relato. O uso anterior de `84`
não demonstra por si só um erro em linux-x64.

### 3.3. Desenho sobre a tela atual

**Prioridade: alta.**

- [x] Reproduzir o fundo incorreto com cena conhecida no E2E X11, isolando
      captura BGRX correta e perda de alfa no caminho SKImage do zoom.
- [x] Corrigir a perda da captura normalizando o byte de alfa para 255;
      manter branco/preto apenas como modos explicitamente acionados por W/K.
- [x] Implementar o caminho de desenho transparente no X11 quando há compositor,
      usando janela alfa e sem recapturar a própria sobreposição.
- [x] Usar captura congelada explicitamente quando não há compositor X11; manter
      a captura congelada para zoom.
- [ ] Validar viabilidade no Wayland e documentar capacidades por compositor.
      Captura congelada deve ser uma alternativa explícita, nunca apresentada
      como equivalente a transparência real.
- [x] Manter captura congelada como comportamento do zoom; não ampliar este item
      para implementar todo o Live Zoom previsto no plano geral.
- [x] Concluir o aceite manual de transparência, zoom, PNG com alfa e uso nos
      outros monitores da configuração GNOME X11 informada pelo solicitante.

**Validação que pode refutar o diagnóstico:** desenhar sobre uma janela com
conteúdo reconhecível em movimento. Uma imagem estática correta valida apenas o
caminho de captura, não a transparência.

**Aceite:** traços visíveis sobre o conteúdo atual, sem fundo opaco indevido;
W/K continuam opcionais; escala, recorte e múltiplos monitores são verificados.
Ambientes sem suporte devem ter limitação e alternativa explicitamente registradas.

**Estado da validação:** E2E X11 com/sem compositor passou, incluindo conteúdo
animado atrás dos traços, alfa e fallback opaco. O solicitante confirmou desenho
transparente, zoom e PNG na sessão real, sem forçar visual/driver SDL, e depois
confirmou uso nos outros monitores, autorizando a conclusão desses testes.
O [registro de aceite](plano-e2e-grafico-uv.md#aceite-manual-do-desenho-transparente-em-gnome-x11)
identifica ambiente, hash e análise do PNG. O aceite abrange os dois monitores
1920×1080 em escala 1 informados; escala fracionária, outras topologias e Wayland
não estão cobertos por essa confirmação.

### 3.4. Ícone próprio e distribuição

**Prioridade: média; necessária para finalizar a tray.**

- [x] Criar ícone original do PixelPointer, legível em tamanhos pequenos e em
      temas claro/escuro, sem reutilizar a identidade de terceiros.
- [x] Preparar recursos para tray, janela e entrada desktop, com formatos e
      resoluções adequados aos consumidores. Tray e janelas compartilham o ícone
      rasterizado; a entrada desktop usa SVG.
- [x] Incluir os recursos nos pacotes e na instalação por usuário; substituir a
      referência ao ícone genérico no desktop file gerado pela CLI e pelo pacote.
- [x] Fornecer e validar a remoção dos recursos instalados por usuário com
      `uninstall-desktop-file`, preservando arquivos modificados, links e recursos
      de outras instalações. O pacote continua sob seu gerenciador de pacotes.

**Aceite:** instalação limpa apresenta o ícone correto sem depender do tema;
remoção não apaga conteúdo alheio; pacotes continuam passando pelo smoke test.

**Estado da validação:** pixels do ícone cobertos em CPU com Skia; instalação,
idempotência, conflito e remoção por usuário exercitados com filesystem real,
inclusive no pacote AOT. O print fornecido pelo solicitante em 2026-10-30 confirma
o ícone próprio visível no painel GNOME. Outros temas/tamanhos e o ícone de janela
dependem do backend SDL e permanecem sem aceite específico.

### 3.5. Tray e ciclo de vida

**Prioridade: média.**

- [x] Verificar suporte às APIs de tray na versão adotada de SDL3 e nos binários
      distribuídos, reutilizando a biblioteca existente quando viável.
- [x] Integrar processamento de eventos à espera por comandos, mantendo SDL na
      thread principal e evitando que a fila bloqueante deixe a tray sem resposta.
- [x] Adicionar menu com **Zoom**, **Desenhar** e **Sair**.
- [x] Manter o daemon ativo ao fechar a sobreposição; impedir múltiplas sessões
      visuais conflitantes e instâncias duplicadas.
- [x] Fazer **Sair** cancelar também uma sobreposição ativa e liberar tray,
      atalhos, socket e conexões sem exigir Ctrl+C.
- [x] Detectar ausência de suporte à tray. No GNOME, verificar a necessidade de
      extensão e documentar a alternativa sem instalar extensões silenciosamente.
- [x] Confirmar visualmente o ícone e a abertura do menu Zoom/Desenhar/Sair na
      tray GNOME, conforme print fornecido pelo solicitante em 2026-10-30.
- [x] Concluir o aceite manual de encerramento por Sair na tray GNOME, conforme
      confirmação do solicitante e print de status sem daemon em 2026-10-30.

**Aceite:** abrir, fechar e reabrir pela tray; sair com e sem sobreposição ativa;
não travar, duplicar processos ou deixar recursos residuais. Quando a tray não for
suportada, informar a limitação e manter controle por CLI.

**Estado da validação:** além do smoke SDL dummy e da reprodução D-Bus isolada,
o E2E gráfico persistente verifica pixels do ícone/menu, cliques XTEST reais em
Zoom/Desenhar/Sair, saída e reinício. A variante sem host StatusNotifier confirma
a limitação e a alternativa por CLI; quit com sobreposição aberta também passou.
O print complementar do solicitante comprova o ícone visível no painel GNOME e o
menu aberto com as três opções corretas. Em seguida, o solicitante confirmou
“Encerramento por Sair funciona” e forneceu print com retorno ao prompt e
`status` respondendo “Daemon não está em execução”. O fluxo manual tray → Sair
fica concluído nesse ambiente. Zoom/Desenhar acionados pela tray ainda não foram
confirmados explicitamente; o print não informa código de saída nem inspeção
individual de sockets/grabs, nem distingue saída com e sem sobreposição aberta.

### 3.5.1. Regressão reproduzida após o teste no Ubuntu/GNOME X11

O solicitante relatou abort do daemon após anunciar a tray, com a asserção
`_dbus_check_is_valid_utf8` da libdbus, no Ubuntu 24.04.5, GNOME X11, x64, com
compositor presente e dois monitores. O encerramento do processo libera os grabs
X11; portanto, a ausência de resposta a Ctrl+1/Ctrl+2 nesse teste não comprova
uma segunda falha nos atalhos. Registry indisponível e GlobalShortcuts ausente
não causaram esse abort no caminho X11.

**Hipótese e refutação:** o backend D-Bus do SDL retém os ponteiros dos textos do
menu além da chamada nativa; o marshaling temporário de `string` os liberava
antes da consulta pelo painel. A hipótese foi testada consultando o menu por
D-Bus privado, sem acessar a sessão gráfica do usuário: o binário anterior
reproduziu a mesma asserção, e o corrigido preservou os textos.

- [x] Manter os textos UTF-8 em memória nativa, por `SafeHandle`, até depois de
      `SDL_DestroyTray`, em [TrayController](../src/PixelPointer.App/TrayController.cs).
- [x] Anexar entradas com a posição pública `-1`, evitando também duplicação
      reproduzida na inserção por índice dessa versão do SDL.
- [x] Adicionar quatro regressões persistentes de tempo de vida, terminador UTF-8,
      texto acentuado e liberação idempotente em
      [TrayLabelTests](../tests/PixelPointer.UnitTests/TrayLabelTests.cs).
- [x] Distinguir tray **registrada** de ícone efetivamente visível no desktop,
      mantendo diagnóstico e recuperação pós-abort no [guia de uso](uso.md).

**Fontes verificadas:** SDL fixado pelo pacote `ppy.SDL3-CS` `2026.1002.1`,
commit `ea7a2dabfd1ab6b0a5720568ffd207f5825e47f1`:
[retenção do texto no backend D-Bus](https://github.com/libsdl-org/SDL/blob/ea7a2dabfd1ab6b0a5720568ffd207f5825e47f1/src/tray/unix/SDL_dbustray.c)
e [inserção na lista](https://github.com/libsdl-org/SDL/blob/ea7a2dabfd1ab6b0a5720568ffd207f5825e47f1/src/SDL_list.c).

**Evidência após a correção:** 58 testes aprovados, build sem avisos/erros,
formatação/análise e Native AOT aprovados. No binário distribuído reconstruído,
dez consultas `GetLayout` retornaram exatamente **Zoom**, **Desenhar**, **Sair**;
`IconPixmap` retornou 32×32 pixels, 4096 bytes; o evento D-Bus de clique em
**Sair** encerrou o daemon normalmente. Essa reprodução diagnóstica inicial foi
seguida pela suíte E2E persistente e por 59 testes unitários aprovados.
O reteste do solicitante no Ubuntu/GNOME X11 manteve o daemon ativo durante
desenho, zoom e salvamentos, sem o abort anterior. O print posterior confirma
também ícone visível e abertura do menu real com textos corretos. O acionamento
de Sair foi depois confirmado pelo solicitante, com retorno ao prompt e status
sem daemon. O encerramento pela tray está aceito manualmente; registro, exibição
do menu e execução de Zoom/Desenhar são verificações distintas.

### 3.6. Segundo plano, IPC e orientação ao usuário

**Prioridade: média.**

- [x] Disponibilizar abertura pelo menu de aplicativos sem terminal e sem
      esconder a entrada destinada ao usuário.
- [x] Informar na execução pelo terminal que Esc fecha a sobreposição, enquanto
      Ctrl+C encerra o daemon; orientar sobre a execução independente do terminal.
- [x] Implementar os comandos propostos `status` e `quit` via IPC, com respostas
      claras e sem iniciar uma sobreposição quando não existir daemon.
- [x] Proteger a instância única: um segundo daemon não pode remover o socket
      pertencente ao processo ativo.
- [x] Validar e autorizar caminhos e acesso ao IPC, restringindo o controle ao
      usuário da sessão e tratando sockets obsoletos sem exclusão indiscriminada.
- [x] Definir eventual início automático como opção explícita, separada do fluxo
      obrigatório de segundo plano.
- [x] Atualizar ajuda e [guia de uso](uso.md), incluindo ambientes sem tray,
      permissões, diagnóstico e encerramento.

**Aceite:** iniciar pelo desktop sem terminal, usar atalhos, fechar somente a
sobreposição, consultar o estado e encerrar pela tray ou CLI. A saída libera
recursos e permite reinício. O aplicativo não deve depender de manter um terminal
aberto quando iniciado pelo fluxo de desktop.

**Estado da validação:** instalação desktop idempotente, preservação de conflito,
`status`, `quit`, saída limpa e remoção do socket foram exercitados em container.
O E2E gráfico também verifica clique no launcher sintético que interpreta a
entrada instalada. Atalhos reais e modificadores foram confirmados pelo
solicitante; abertura pelo menu GNOME sem terminal ainda não foi confirmada.
`daemon` permanece em primeiro plano no terminal, conforme o [README](../README.md#primeiros-passos);
não há desacoplamento automático nem início automático com a sessão.

### 3.7. Pasta de destino das capturas com Ctrl+S

**Requisito adicional do solicitante; implementado, com validação nativa por SO
parcial.**

O Ctrl+S deve salvar o PNG na pasta de capturas do usuário atual, consultando as
configurações do sistema operacional em vez de usar o diretório de trabalho ou
montar caminhos fixos com nomes de usuário. A resolução deve respeitar pastas
redirecionadas, nomes localizados e configurações personalizadas.

| Sistema | Regra de resolução | Exemplo ilustrativo, não codificado |
| --- | --- | --- |
| Windows | Resolver a Known Folder `FOLDERID_Pictures` com `SHGetKnownFolderPath()` ou API equivalente e usar sua subpasta `Screenshots`, respeitando redirecionamentos, inclusive para OneDrive | `C:\Users\<usuario>\OneDrive\Pictures\Screenshots` |
| macOS | Consultar a preferência `location` do domínio `com.apple.screencapture`; quando ela não estiver definida, resolver a Área de Trabalho do usuário pela API do sistema | `~/Desktop` ou o destino personalizado das capturas |
| Linux, tanto X11 quanto Wayland | Resolver `PICTURES` conforme XDG User Directories e usar a subpasta `Screenshots`; consultar `user-dirs.dirs` sob `$XDG_CONFIG_HOME` ou, na ausência dessa variável, `~/.config`, respeitando `XDG_PICTURES_DIR` | `~/Pictures/Screenshots` ou `<pasta de imagens configurada>/Screenshots` |

No Linux, essa é a política do PixelPointer, não uma afirmação de que GNOME, KDE,
XFCE e ferramentas de terceiros compartilham um único destino de capturas.
Não é necessário copiar a configuração própria de cada ferramenta. A configuração
XDG deve ser interpretada sem executar o arquivo como script; expandir somente as
formas de referência ao diretório pessoal previstas pelo padrão.

- [x] Implementar resolução específica por sistema, compartilhando a lógica de
      gravação do PNG e mantendo compatibilidade Native AOT.
- [x] Consultar a configuração no momento do salvamento, sem alterar preferências
      do sistema, reiniciar serviços ou exigir execução de comandos pelo usuário.
- [x] Normalizar e autorizar o destino antes de acessar o filesystem; resolver `~`
      quando aplicável à preferência do macOS e rejeitar destinos relativos ou
      configurações malformadas. No Linux, uma pasta de imagens ausente ou
      desabilitada deve gerar orientação explícita, não um destino adivinhado.
- [x] Criar a pasta de destino quando necessária e autorizada. Se a configuração
      existir, mas o destino estiver indisponível ou sem permissão, informar a
      falha sem salvar silenciosamente em outra pasta ou no diretório de trabalho.
- [x] Preservar capturas existentes com criação exclusiva e nomes sem colisão,
      inclusive em salvamentos no mesmo segundo; nunca sobrescrever arquivos.
- [x] Confirmar o salvamento e informar o caminho final ao usuário, inclusive
      quando iniciado pelo menu sem terminal; uma falha não pode ser apresentada
      como sucesso nem encerrar o daemon ou a sobreposição.
- [x] Atualizar a ajuda e o [guia de uso](uso.md) com a nova política de destino,
      preservação de arquivos e feedback não modal na sobreposição.
- [ ] Testar resolução e gravação com filesystem real em containers apropriados
      por SO: pastas redirecionadas, caminhos com espaços e acentos, configuração
      ausente ou inválida, diretório sem permissão e colisões de nomes.

**Validação que pode refutar a implementação:** configurar um destino diferente
do padrão, iniciar a aplicação com outro diretório de trabalho e acionar Ctrl+S;
o PNG deve aparecer somente no destino resolvido. Repetir com Pictures
redirecionada no Windows, preferência personalizada e ausente no macOS e
`XDG_PICTURES_DIR` personalizado no Linux. Tornar o destino indisponível deve
produzir erro explícito, sem gravação alternativa.

**Aceite:** Ctrl+S respeita a configuração do usuário em cada SO, informa o destino
efetivo e preserva arquivos existentes. A aplicação não modifica configurações do
sistema nem depende do terminal ou do diretório de onde foi iniciada.

**Estado atual:** [SnapshotStorage](../src/PixelPointer.App/SnapshotStorage.cs)
resolve o destino e grava arquivos exclusivos, com modo 0600 em Linux/macOS;
[ScreenshotPreferences](../src/PixelPointer.App/MacOS/ScreenshotPreferences.cs)
consulta Core Foundation sem executar comandos ou alterar preferências.
[SaveSnapshot](../src/PixelPointer.App/Overlay/OverlaySession.cs) informa sucesso
ou falha na sobreposição sem bloquear o processamento de saída.

**Evidência:** testes exercitam XDG com filesystem real, caminhos com espaços e
acentos, configuração inválida/ausente/desabilitada, expressões executáveis
rejeitadas, nomes distintos, preservação e limpeza de gravação incompleta. PNG e
alfa são verificados com Skia em CPU. As regras de preferência/fallback do macOS
possuem testes de lógica. E2E X11 verifica Ctrl+S repetido, feedback de erro,
ausência de destino alternativo e recuperação; o solicitante confirmou os PNGs
de zoom e desenho na sessão GNOME X11. Consulta nativa macOS, Known Folder Windows
e feedback gráfico nesses dois sistemas ainda precisam de validação.

O salvamento mantém o conteúdo da cena: captura congelada com anotações no zoom,
quadro em W/K ou somente anotações com alfa no desenho transparente. Não recaptura
o desktop ocultamente nem inclui cursor/mensagem de confirmação no PNG.

### 3.8. Limites de confiança e revisão das alterações

- Capturas permanecem em memória até ação explícita; Ctrl+S grava apenas no
  destino absoluto resolvido, sob as permissões do usuário, sem envio pela rede.
- XDG é dado, não script: não há shell, substituição de comandos ou expansão
  arbitrária. Configuração inválida ou destino sem acesso falham explicitamente.
- Instalação/remoção desktop comparam conteúdo antes de agir e rejeitam links,
  inclusive em componentes do caminho. A remoção precisa da mesma instalação e
  configuração de dados; não remove diretórios nem PNGs.
- O IPC usa diretório privado, socket/lock restritos, comandos conhecidos e prazos
  limitados. Outro cliente incompleto não pode manter indefinidamente o servidor
  ocupado. Falha de comunicação não inicia silenciosamente outra sobreposição.
- Falha de instância única é observada antes de anunciar o daemon; uma segunda
  instância não remove o socket ativo. Socket não responsivo exige verificação
  explícita, nunca exclusão automática.
- O usuário da sessão e suas preferências são a fronteira de confiança; não há
  elevação de privilégios nem tentativa de contornar permissões de portal/TCC.
  Essa revisão de escopo e seus testes não substituem o aceite nos desktops reais.

## 4. Compatibilidade X11 e Wayland

| Aspecto | Diretriz |
| --- | --- |
| Código comum | CLI, comandos, estado do daemon, modelo de desenho e renderização compartilhável |
| Código específico | Adaptadores de captura, atalhos, permissões e capacidades de sobreposição |
| Seleção | Em tempo de execução, considerando a sessão e capacidades efetivas |
| Distribuição | Mesmo executável por SO/arquitetura, enquanto tecnicamente viável |
| Exceção | Builds distintos apenas após reproduzir e documentar incompatibilidade impeditiva |
| Segurança | Não usar um backend como forma de contornar permissões do outro |

Uma nova dependência, se indispensável, exige versão fixada, manutenção verificada,
licença permissiva compatível com MIT e avaliação de compatibilidade Native AOT.
Decisões arquiteturais duradouras devem seguir o processo de ADR do projeto.

## 5. Estratégia de validação

1. [x] Criar regressões focadas para ABI, classificação de erros, IPC e filesystem.
2. [x] Executar testes unitários e de filesystem/IPC, incluindo PNG/ícone em CPU
      (59 aprovados na validação registrada com o E2E).
3. [x] Exercitar IPC e filesystem com sockets Unix e diretórios reais em
      containers; menu SNI, servidor X11 e conflitos reais de grab cobertos pelo E2E.
      Integração com a matriz de portais permanece pendente.
4. [x] Verificar desenho, zoom, salvamento e modificadores no E2E X11 e concluir
      o aceite manual desses fluxos no Ubuntu/GNOME X11, incluindo uso nos outros
      monitores, conforme confirmação explícita do solicitante em 2026-10-30.
5. [ ] Testar Wayland com portais presentes/ausentes e desktops com/sem tray, mantendo
   uma matriz explícita de capacidades e limitações.
6. [x] Executar formatador, análise estática, build, testes e smoke Native AOT
   para linux-x64. Regressões em Windows e macOS permanecem pendentes.
7. [x] Validar os pacotes linux-x64 em Ubuntu 22.04, Rocky Linux 9 e Debian 12,
   incluindo .deb, SBOM, checksum, recursos, conflito, remoção, instância única,
   encerramento e reinício do daemon com SDL dummy.
8. [ ] Completar o aceite manual do acionamento de Zoom/Desenhar na tray e
      da abertura pelo menu GNOME sem terminal; revisar vídeos/referências do E2E.
      Ícone, abertura do menu e encerramento por Sair já confirmados pelo solicitante.

### Matriz de evidências

| SO/arquitetura | Sessão ou backend | Resultado |
| --- | --- | --- |
| Linux x64, SDK em container Ubuntu | Sem tela; Skia em CPU | Formatador/análise, build e 59 testes aprovados; 0 avisos/erros |
| Ubuntu 22.04, x64 | SDL dummy, host StatusNotifier e D-Bus privados | Abort UTF-8 reproduzido antes; após correção, dez consultas ao menu, ícone 32×32 e clique em Sair aprovados |
| Ubuntu 22.04, Rocky Linux 9 e Debian 12, x64 | SDL 3.5.0 dummy, Native AOT | Pacote, bibliotecas, desktop por usuário, conflitos, remoção, instância única, status/quit e reinício aprovados |
| Ubuntu 22.04 e Debian 12, x64 | Pacote `.deb`, SDL dummy | Instalação/remoção, ícone e selftest aprovados |
| Linux x64, X11 virtual | twm, painel SNI sintético, compositor opcional | 10/10 consecutivos, variantes sem compositor/sem tray e falha controlada verificadas; detalhes no plano E2E |
| Ubuntu 24.04.5/GNOME X11, x64 | Compositor presente, dois monitores 1920×1080 em escala 1 | Aceite manual concluído para desenho transparente, visual automático, zoom, PNG, CapsLock/NumLock e uso nos outros monitores; prints complementares do teste no monitor 1 e do ícone/menu da tray fornecidos nesta conversa |
| Ubuntu/GNOME X11, x64 | Painel, launcher e cenários adicionais | Ícone/menu visíveis e Sair confirmados, com status sem daemon após encerramento; Zoom/Desenhar pela tray, menu de aplicativos sem terminal, conflito físico e outras escalas/topologias permanecem sem confirmação manual |
| Linux Wayland, por compositor | Portais com/sem Registry, permissões e tray | Pendente de integração/roteiro manual |
| Windows x64/arm64 | Known Folder redirecionada, SDL Win32 | Implementação compilada; validação nativa pendente |
| macOS x64/arm64 | Preferência de captura, Desktop e SDL Cocoa | Lógica de resolução coberta; Core Foundation e validação nativa pendentes |

O [roteiro X11 no guia de uso](uso.md#roteiro-manual-de-aceite-no-x11) separa
atalhos físicos, conflito, imagem congelada, alfa, tray, escala e múltiplos
monitores. Testes automatizados não acessam a sessão gráfica do usuário.

Runners e ferramentas de build devem executar em containers. Para interfaces
nativas SDL, usar testes gráficos adequados; Playwright não substitui interação
nativa. Caso exista superfície HTTP de preview ou automação, navegador e runner
devem acessá-la exclusivamente por nginx também em container.

Para a validação ampla existente, executar a partir da raiz do repositório, com
PowerShell 7, Podman e acesso às dependências de build:

```bash
pwsh ./tools/scripts/Invoke-Validation.ps1 -Aot -Engine podman
```

O resultado esperado é sucesso nas etapas de validação e no smoke do binário
Native AOT. O comando cria artefatos e cache de dependências locais; não comprova
sozinho comportamento de tray, atalhos físicos ou transparência.

## 6. Ordem e conclusão

Ordem proposta: compatibilidade de portais, atalhos X11, desenho sobre a tela,
ícone, tray e controle em segundo plano. A segurança de instância única e IPC
deve estar resolvida antes de disponibilizar múltiplos caminhos de inicialização.
O requisito adicional de destino das capturas com Ctrl+S está implementado na seção
3.7; seu aceite gráfico e a resolução nativa em Windows/macOS ainda são necessários.

- [x] Desenho transparente, zoom, PNG com alfa, CapsLock/NumLock e uso nos monitores
      testados possuem aceite manual concluído, autorizado pelo solicitante.
- [x] Ícone visível e menu Zoom/Desenhar/Sair aberto na tray GNOME possuem
      evidência visual fornecida pelo solicitante, junto aos prints do monitor 1.
- [ ] Completar os demais critérios da issue; o aceite dos fluxos acima não
      encerra a matriz de portais, tray/launcher e plataformas restantes.
- [x] O fluxo **tray → Sair** possui aceite manual no GNOME X11, com confirmação
      do solicitante e print de retorno ao prompt e status sem daemon.
- [ ] A abertura pelo menu de aplicativos sem terminal possui aceite GNOME real;
      passou no E2E com launcher sintético, mas falta confirmação manual.
- [x] As capacidades X11/Wayland estão registradas sem prometer paridade não testada.
- [x] Testes focados, verificações amplas e smoke dos pacotes relevantes passaram.
- [x] Ajuda e documentação refletem o comportamento efetivamente entregue.
- [ ] Ctrl+S salva na pasta resolvida pela configuração do usuário em cada SO,
      conforme a seção 3.7, sem sobrescrita ou redirecionamento silencioso.
- [ ] Evidências sanitizadas estão vinculadas à issue antes de seu encerramento.

O diagnóstico inicial foi elaborado por análise estática. Na execução foram
implementadas as correções assinaladas; passaram build, análise/formatador, 59
testes, Native AOT, smoke de pacotes linux-x64 e E2E gráfico X11. O solicitante
autorizou concluir o recorte manual de desenho, zoom, PNG, CapsLock/NumLock e uso
nos monitores testados. Evidências, ambiente e limites estão no
[plano E2E](plano-e2e-grafico-uv.md#aceite-manual-do-desenho-transparente-em-gnome-x11).
Acionamento de Zoom/Desenhar pela tray e launcher reais, matriz de portais/Wayland,
validações Windows/macOS e vinculação das evidências à issue permanecem pendentes;
a issue não foi encerrada. A visibilidade do ícone/menu GNOME e o encerramento
por Sair estão confirmados pelo solicitante.
