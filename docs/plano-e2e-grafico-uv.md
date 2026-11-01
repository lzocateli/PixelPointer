# Plano de E2E gráficos isolados com uv e evidência em vídeo

**Português (Brasil)** · [English](en/plano-e2e-grafico-uv.md) · [Español](es/plano-e2e-grafico-uv.md)

| Campo | Estado |
| --- | --- |
| Data | 2026-10-30 |
| Autorização humana | Solicitante, nesta conversa: “Eu autorizo os E2E gráficos isolados nas regras do projeto” |
| Decisão autorizada | Permitir uma suíte gráfica automatizada separada e isolada |
| Estado de execução | X11 executado com estabilidade 10/10; revisão humana dos vídeos encerrada por decisão do mantenedor em 2026-10-31, sem revisão registrada. ScreenCast/PipeWire validado em Sway isolado com render node e caminho sem GPU tratado. Captura, zoom e desenho da preview v0.1.3-preview.14 aceitos manualmente no Ubuntu 26.04.1/GNOME Wayland em 2026-10-31; token, indicador, latência sem diálogo e matriz adicional continuam pendentes |
| Versão funcional de referência | v0.1.2, primeira versão funcional aceita como estável pelo mantenedor em 2026-10-30 nos ambientes GNOME X11 e Wayland registrados; não é aprovação da matriz multiplataforma completa |
| Primeiro alvo | Linux x64, X11 em desktop descartável |
| Expansão | GNOME X11, Wayland por compositor e demais sistemas após viabilidade comprovada |

## 1. Objetivo e limites

Exercitar o pacote Native AOT como um usuário: iniciar, acionar atalhos globais,
desenhar, salvar, interagir com a tray e encerrar. Gerar resultados verificáveis,
vídeo e dados suficientes para repetir uma execução e diagnosticar regressões.

O [README](../README.md) define o produto; o [guia de uso](uso.md) define seus
comandos e comportamentos. As [regras de testes](../.github/instructions/testing.instructions.md)
registram a autorização. Este plano não substitui esses contratos nem o
[plano da issue #3](plano-correcao-issue-3.md).

Não implementar funcionalidades futuras apenas para testá-las. Timer, Live Zoom,
gravação pelo próprio produto, recorte e DemoType não entram na cobertura inicial.
A gravação deste plano pertence ao ambiente de testes, não ao PixelPointer.

## 2. Hipóteses e validações capazes de refutá-las

| Hipótese | Como refutar |
| --- | --- |
| Entrada X11 simulada exercita o caminho global real | Com o foco em outra janela, enviar Ctrl+1/Ctrl+2 pelo servidor X e exigir abertura da sobreposição; comando IPC não substitui esse teste |
| Ambiente virtual suporta desenho transparente | Animar uma região do fundo, manter um traço sobre ela e verificar mudança do fundo com preservação do traço; imagem estática ou SDL `dummy` não comprovam isso |
| Tray funciona do ponto de vista do usuário | Localizar o ícone no painel e clicar fisicamente nas entradas; consulta ou evento D-Bus isolado comprova somente protocolo |
| Vídeo documenta toda a interação | Gravar a tela antes da primeira ação, conferir frames decodificáveis, duração e presença das etapas finais |
| Suíte identifica regressões | Provocar falha controlada, como encerrar o daemon, e exigir resultado reprovado com evidências preservadas |
| Execução é repetível | Repetir a partir de ambiente limpo, com as mesmas versões e referências, sem intervenção manual |

## 3. Ambiente e isolamento

Arquitetura implementada no primeiro incremento:

```text
Docker/Podman no host: somente orquestração
├── Desktop de teste em container
│   ├── Servidor X11 virtual
│   ├── Gerenciador de janelas, compositor e painel StatusNotifier
│   ├── D-Bus privado e configurações descartáveis
│   ├── Pacote Native AOT e cena visual sintética
│   ├── Runner Python executado com uv
│   └── Captura da tela e codificação do vídeo
└── nginx em container: borda exclusiva de qualquer serviço HTTP
```

- Não montar sockets X11/Wayland, D-Bus, diretório pessoal, secrets ou dispositivos
  de entrada do host. Não usar `--privileged`, rede do host ou injeção global no host.
- Usar usuário sem privilégios, rede privada, limites de recursos e prazo global.
  Montar o pacote somente para leitura e limitar escrita ao diretório da execução.
- Isolar `HOME`, configurações XDG, runtime, capturas e preferências por cenário.
  Cleanup não pode apagar recursos de outra execução, mesmo após falha.
- Xvfb é candidato ao servidor X11, não um desktop completo. Confirmar recursos
  necessários de composição/renderização antes de aprovar a imagem.
- Manter nginx na execução com aplicação. Se houver visualização remota ou
  auxiliares HTTP, navegador e runner acessam apenas essa borda, sem portas diretas.
  Automação nativa e D-Bus usam canais privados, não são transformados em HTTP.
- Não exigir serviço HTTP, navegador ou Playwright para controlar a janela SDL.
  Visualização remota é opcional e não condiciona a primeira entrega.

## 4. Runner, dependências e reprodutibilidade

O projeto Python está em `tests/e2e/`, com preparação e coleta de evidências
compartilhadas. O [runbook](../tests/e2e/README.md) registra comandos, versões,
licenças, ameaças e recuperação. Python, manifesto e lockfile estão fixados;
o build usa `uv sync --locked` e o runtime offline usa `uv run --locked --no-sync`,
sem atualização automática de dependências.

Antes de escolher cada componente:

1. Verificar licença permissiva da versão exata e das dependências transitivas,
   manutenção e compatibilidade com a distribuição MIT.
2. Fixar versões de `uv`, Python, ferramentas nativas e imagem por digest.
3. Registrar fontes, licenças e versões; bloquear componentes sem licença verificada.

Não assumir que uma distribuição de FFmpeg, codecs ou biblioteca de automação é
permitida. Selecionar gravador e codec somente depois dessa verificação. O `uv`
resolve dependências Python; não fixa sozinho servidor gráfico, fontes ou codecs.

No X11, preferir eventos XTEST pelo servidor gráfico. Não usar chamadas internas
ou eventos SDL injetados como substitutos de atalhos globais. Coordenadas devem
ser relativas a janelas/regiões conhecidas, em geometria controlada.

Fixar resolução, escala, tema, fontes, locale, fuso e renderização. A cena sintética
deve conter formas, cores e uma região animada determinística, sem dados pessoais.
Esperar condições com deadlines; evitar sequências baseadas apenas em `sleep`.

## 5. Matriz de cenários das funcionalidades atuais

| Grupo | Ações | Asserções obrigatórias |
| --- | --- | --- |
| Daemon e IPC | Iniciar, consultar status, tentar segunda instância | Disponibilidade, recusa da duplicação e preservação do primeiro processo |
| Atalhos globais | Ctrl+1/Ctrl+2 com foco externo, CapsLock/NumLock alternados | Janela e modo esperados; sem perder o daemon |
| Conflito | Outro cliente de teste registra uma combinação antes do daemon | Erro explícito; não anunciar grab indisponível como sucesso |
| Zoom | Roda, setas e movimento do mouse | Escala e região da captura mudam conforme a ação |
| Anotações | Arrastar; R/G/B/Y/O/P; Ctrl+roda | Geometria, cores e espessuras dentro de tolerâncias definidas |
| Edição | Ctrl+Z e E | Remoção do último traço e de todos os traços, respectivamente |
| Quadros | W e K | Fundo branco/preto e comportamento de anotações conforme contrato |
| Transparência | Desenhar sobre cena animada com compositor | Fundo segue mudando; traços persistem; PNG contém alfa |
| Sem compositor | Repetir desenho em ambiente sem compositor | Aviso explícito e captura congelada, sem alegar transparência |
| Salvamento | Ctrl+S repetido, destino XDG personalizado com espaços e acentos | PNG decodificável, pasta correta, nomes exclusivos e arquivos anteriores intactos |
| Configurações | `settings.json` com cor, espessura e zoom inicial; depois arquivo inválido | `probe` carrega o arquivo; zoom 4x, traço azul espesso; arquivo inválido gera aviso, usa padrões e permanece intacto |
| Falha de salvamento | Configuração inválida ou destino sem acesso | Feedback de erro visível, ausência de destino alternativo e processo utilizável |
| Saída da sobreposição | Esc e botão direito nos modos aplicáveis | Transições conforme contrato, possibilidade de reabrir e daemon preservado |
| Tray gráfica | Clicar no ícone e nas opções Zoom, Desenhar e Sair | Ícone visível, menu correto, ação correspondente e encerramento limpo |
| Tray indisponível | Iniciar sem host StatusNotifier | Limitação explícita; controle por status/quit preservado |
| Ciclo de vida | Quit com sobreposição aberta, reinício e abertura pelo menu desktop | Recursos liberados; nova execução funcional; início sem terminal |
| CLI e desktop | Help, version, probe, capture e instalação/remoção por usuário | Saída/arquivo coerentes, identidade própria e preservação de conflitos |

Reutilizar os testes existentes de filesystem, IPC, pacote e PNG, sem duplicá-los
integralmente. O E2E acrescenta a interação observável, inclusive mensagens na tela.
Grant e permissões de portal pertencem à expansão Wayland, não ao aceite X11.
Pressão de caneta, escala fracionária e múltiplos monitores exigem cenários próprios;
mouse e um único monitor virtual não os validam.

## 6. Evidências e critérios de aprovação

Cada execução deve possuir identificador exclusivo e diretório de artefatos próprio,
sem sobrescrever resultados anteriores, contendo:

- vídeo por cenário, capturado do desktop, não montado a partir dos PNGs do produto;
- screenshots antes/depois das verificações e PNGs exportados por Ctrl+S;
- relatório legível e JUnit XML para integração com CI;
- linha do tempo estruturada com ação, horário monotônico e verificação;
- manifesto com hash do pacote, digest da imagem, versões e parâmetros gráficos;
- diagnóstico sanitizado e restrito ao teste, sem logs integrais ou ambiente completo.

Iniciar o gravador antes das ações e verificar prontidão. Encerrá-lo corretamente
também em falha, preservando vídeo parcial quando recuperável. Falha de gravação
obrigatória reprova a execução; não aceitar vídeo vazio ou sucesso sem evidência.
Não incluir áudio por padrão.

O primeiro incremento deve fixar e versionar os seguintes critérios:

- código zero somente se todas as asserções e evidências obrigatórias passarem;
- timeout, crash ou funcionalidade obrigatória ignorada não podem produzir sucesso;
- pontos de verificação visuais com regiões, tolerâncias e referências explícitas,
  sem atualizar imagens de referência automaticamente;
- PNGs analisados quanto a dimensão, cor/alfa e conteúdo, não apenas existência;
- vídeo decodificável, cobrindo o intervalo das ações; tolerância de duração definida
  em função da taxa de quadros escolhida;
- dez execuções consecutivas limpas da prova de conceito sem falhas intermitentes;
- falha controlada detectada, relatório reprovado e evidências úteis preservadas.

Retentativas não devem esconder a primeira falha. Registrar cada tentativa e não
reclassificar uma execução instável como aprovação estável. Definir orçamento de
tempo e retenção dos vídeos na implementação; preservar falhas até análise e não
publicar gravações automaticamente.

## 7. Etapas e entregáveis

1. **Inventariar contratos e escolher componentes.** Validar licenças, versões,
   backend de entrada, captura e capacidade de compositor/painel. Entregar escolhas
   verificáveis antes de adicionar dependências.
2. **Construir ambiente mínimo.** Desktop descartável, cena sintética e runner uv;
   fazer build e smoke da imagem, verificar isolamento e limpeza em erro.
3. **Prova de conceito vertical.** Daemon → Ctrl+1 → desenho → Ctrl+S → Esc →
   ícone/menu → Sair. Entregar vídeo, asserções e execução limpa repetível.
4. **Demonstrar detecção de regressão.** Forçar falha em subprocesso da execução;
   verificar código não zero, vídeo e relatório. Se viável, usar pacote anterior
   conhecido como defeituoso sem modificar nem reverter o worktree atual.
5. **Expandir X11.** Cobrir a matriz da seção 5, com/sem compositor e tray,
   conflitos, modificadores e erros de salvamento.
6. **Integrar execução e evidências.** Documentar comando único a partir da raiz,
   pré-requisitos, resultados e recuperação. Host apenas orquestra containers;
   runner e ferramentas executam dentro deles.
7. **Expandir compatibilidade.** GNOME X11 real isolado, Wayland por compositor,
   monitores/escala e arquiteturas adicionais após prova de suporte.

O comando executável, a preparação da imagem, a execução bloqueada pelo lockfile e
a inspeção dos resultados estão no [runbook](../tests/e2e/README.md).
Não editar workflows `gw-*` nem `tools/scripts/ci/`; mudanças
nessas superfícies pertencem ao catálogo Groundwork. A integração CI deve usar
somente superfícies do projeto autorizadas, após a prova local em containers.

## 8. Compatibilidade, riscos e aceite pendente

| Ambiente | Plano e limite |
| --- | --- |
| Linux x64, X11 mínimo virtual | Executado: 10/10 consecutivos com compositor/tray; variantes sem compositor e sem tray passaram |
| GNOME X11 | Aceite manual concluído: desenho transparente, visual automático, zoom, PNG, CapsLock/NumLock, uso nos dois monitores e encerramento por Sair; prints confirmam monitor 1, ícone/menu da tray e status sem daemon após saída; Zoom/Desenhar pela tray, launcher e matriz completa ainda sem comprovação manual |
| Wayland | ScreenCast/PipeWire passou em Sway isolado; indisponibilidade sem GPU tratada. Preview v0.1.3-preview.14 aceita manualmente para captura, zoom e desenho no Ubuntu 26.04.1/GNOME Wayland, com consentimento observado. Reutilização do token, duração do indicador, latência sem diálogo, multi-monitor e outros compositores pendentes; sem mock de consentimento ou XTEST via XWayland como prova nativa |
| Linux arm64 | Executar em arquitetura apropriada antes de declarar cobertura |
| Windows/macOS | Backends nativos e sessão gráfica descartável próprios; estratégia depende de compatibilidade com a regra de containers e autorização específica se exigir exceção |
| Caneta/GPU/monitores físicos | Uso nos dois monitores 1920×1080 em escala 1 confirmado manualmente; caneta, outras GPUs, escalas e topologias continuam pendentes |

Riscos principais: diferenças de renderização, foco e temporização, painel sem SNI,
compositor virtual sem recursos, vídeo incompleto e dependências incompatíveis.
Falha de infraestrutura deve ser distinguida de falha do produto, mas ambas impedem
aprovação do cenário obrigatório. Não desabilitar transparência para fazer passar
um cenário que promete validá-la.

O aceite final exige a matriz executada com limites explícitos, evidências
reproduzíveis e revisão humana dos primeiros vídeos/referências. A autorização
registrada permite construir a suíte; não aprova antecipadamente ferramentas,
exceções de segurança, suporte multiplataforma ou resultados ainda inexistentes.

### Aceite manual da preview no GNOME Wayland

Em 2026-10-31, o solicitante informou que o teste Linux Wayland passou,
anexou um print e o log da captura, e confirmou em seguida: “Pode registrar
o zoom e desenho como ok.” O teste foi realizado na máquina do solicitante,
não por automação conectada à sua sessão gráfica.

- Ambiente observado: Ubuntu 26.04.1 LTS x64, GNOME/Wayland, Native AOT,
  versão `0.1.3-preview.14` do commit `cdd125e`; ScreenCast v5 e
  GlobalShortcuts v1 informados por `probe`. A versão do GNOME e a escala
  não foram fornecidas explicitamente.
- O print mostra o diálogo de compartilhamento com o monitor interno
  selecionado e a instalação da entrada desktop e do ícone. O aviso de
  registro do app no `probe` ocorreu antes dessa instalação; não há novo
  diagnóstico que comprove sua resolução.
- O log confirma uma captura via **xdg-desktop-portal ScreenCast + PipeWire**,
  PNG de **1920×1080**, portal **20.830 ms**, primeiro quadro **122 ms** e
  total **20.999 ms**. O tempo do portal inclui a escolha e confirmação
  humana no diálogo; não comprova a meta de atalho até quadro < 150 ms.
- Captura, zoom e desenho ficam **aceitos manualmente nesse ambiente**,
  conforme confirmação humana. O print estático não comprova sozinho a
  interação de zoom/desenho; o aceite dessas ações se baseia no relato.
- Permanecem sem evidência desta preview: reutilização do restore token,
  duração do indicador, repetição sem consentimento, atalhos físicos,
  ações da tray/launcher, Ctrl+S, alfa ou conteúdo ao vivo, multi-monitor,
  outros compositores, escalas, GPUs e Windows/macOS. A confirmação não
  conclui a matriz Wayland automatizada nem as fases F2/F3.

O anexo permanece na conversa, sem cópia para o repositório; não se publica
a captura de tela pessoal. O destino pessoal do PNG foi omitido deste registro.

### Validação do diagnóstico por etapas em 2026-10-31

O binário Native AOT local, com o novo diagnóstico ainda não publicado, passou
no teste ScreenCast/PipeWire em Sway isolado com render node. Duas capturas,
cores/formato e quadro novo após mudança da cena foram verificados pelo runner
existente. O nginx ficou isolado, sem portas públicas, com smoke e limpeza;
nenhuma sessão gráfica ou D-Bus do host foi acessada.

Evidências locais: `artifacts/wayland/screencast-20261101T015920989-305aa712`.
Primeira captura: portal 9 ms, remoto 3 ms, leitura 19 ms, campo legado
`primeiro quadro` 23 ms, `restore solicitado nao`. Segunda: portal 8 ms,
remoto 4 ms, leitura 16 ms, campo legado 20 ms, `restore solicitado sim`.
Valores arredondados independentemente; são resultados desta execução, não
benchmark equivalente ao GNOME com diálogo nem comprovação da meta < 150 ms.

O gate local passou com format, build sem avisos/erros, 139 testes unitários,
23 cenários de governança de preview, publicação Native AOT e `selftest`.
A classe ScreenCast teve 23 testes aprovados após preparar Fontconfig no
container. Não foram repetidos testes gráficos nativos Windows/macOS nem
aceite manual GNOME do novo diagnóstico. Este resultado não encerra F2/F3.

### Validação da consulta agrupada em 2026-10-31

O binário Native AOT local, ainda não publicado, passou novamente no Sway
isolado com render node, nginx isolado sem portas públicas e limpeza dos
containers. Evidências: `artifacts/wayland/screencast-20261101T022349873-b4ed0c2d`.
As duas capturas, cores/formato e quadro novo após mudança da cena passaram;
ambas informaram `ScreenCast capacidades: 0 ms; consulta GetAll`, confirmando
o caminho agrupado. O valor arredondado não significa custo zero e não é
benchmark no GNOME.

O gate passou com format, build sem avisos/erros, 156 testes unitários,
23 cenários de governança de preview, Native AOT e `selftest`. Os 24 testes
do cliente de portais cobrem tipos/propriedades, monitor e a sequência antiga
com saídas antecipadas e cancelamento. Respostas incompatíveis e leituras
legadas foram exercitadas como lógica sintética; não houve teste contra um
portal real que rejeite `GetAll`. A matriz GNOME/KDE/outros portais e o
benchmark completo continuam pendentes; esta execução não encerra F2/F3.

### Compatibilidade GetAll sob recusa controlada

Em 2026-11-01, o cenário padrão e o de recusa de `GetAll` passaram usando o
mesmo pacote Native AOT local e a imagem Wayland existente. O nginx esteve
isolado e sem portas públicas, com smoke e limpeza. A sessão gráfica, D-Bus e
diretórios pessoais do host não foram acessados; somente o render node já
autorizado foi exposto ao container pelo modo rootless.

- Controle: `artifacts/wayland/screencast-20261101T121050665-764bf12c`,
  duas capturas ScreenCast/PipeWire com quadro atualizado, sem política adicional.
- Recusa: `artifacts/wayland/screencast-20261101T121052886-6dcf124c`,
  `result.json` com `passed=true`, `mode=getall-denied` e duas rejeições.
  Ambas as capturas continuaram via ScreenCast/PipeWire e informaram `consulta Get`.
- A configuração D-Bus foi derivada da imagem por parser XML com DTD e
  resolução externa desativadas, criada sem sobrescrita e montada somente para
  leitura. A política bloqueia `Properties.GetAll` para o serviço
  `org.freedesktop.portal.Desktop` no container; não examina os argumentos da
  chamada. `Get`, criação/início da sessão e leitura de quadros permanecem permitidos.
- O log do barramento comprova as rejeições. O teste exige os dois diagnósticos
  `Get` e capturas corretas; falha posterior grava `passed=false`, não mantém
  um resultado positivo do runner. Não se substituiu o portal ou seu consentimento.

Comando adicional, na raiz do clone, com pacote atual em `artifacts/aot/linux-x64`,
Podman rootless, imagem Wayland disponível e nginx isolado já iniciado conforme
as regras E2E:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -DenyScreenCastGetAll
```

Exige o diagnóstico de capacidades do novo código, não o pacote da preview
anterior. Sem `RenderNode`, o modo é recusado antes de iniciar o cenário; ajuda
e argumento desconhecido foram testados em container. Não houve mudança de
produção nesta etapa; o cenário padrão também foi repetido e passou.
Cada comando `capture` abre uma conexão nova: este teste não comprova a
preferência `Get` em capturas repetidas numa única conexão do daemon.
Portais naturalmente incompatíveis, GNOME/KDE, matriz multiplataforma e a meta
de latência continuam pendentes. O plano não fica encerrado por esta injeção.

### Capturas repetidas na conexão do daemon

Em 2026-11-01, o cenário opcional `-RepeatDaemonCaptures` passou no controle
e com `-DenyScreenCastGetAll`, usando o mesmo pacote AOT local. O driver Python
usa apenas a biblioteca padrão e ferramentas já presentes na imagem; inicia
o baseline existente e depois um daemon real no mesmo D-Bus/Sway descartável.
Um monitor registra somente chamadas de propriedades ao portal, sem respostas,
tokens ou conteúdo de tela. As capturas visuais inspecionam uma cena sintética.
nginx permaneceu isolado, sem portas públicas, com smoke e limpeza.

| Cenário | Evidências locais | Resultado |
| --- | --- | --- |
| Controle | `artifacts/wayland/screencast-20261101T123222089-3df3f7ae` | Um daemon, duas sessões, duas chamadas `GetAll`, nenhuma recusa |
| Recusa | `artifacts/wayland/screencast-20261101T123225959-89b40b94` | Um daemon, uma `GetAll` recusada, seis `Get` ordenados para versão/fontes/cursor em duas sessões |

O driver identifica a conexão D-Bus pelo PID do daemon. Desenho e zoom são
acionados por IPC, uma janela é exigida e uma captura pelo portal verifica
a cor do quadro; a segunda sessão usa uma cor diferente. A janela é fechada
por `swaymsg`, o processo deve continuar ativo com `status` e `quit` deve
encerrar com código zero. `daemon-result.json` registra chamadas, cores e
ciclo de vida; o orquestrador exige esse resultado completo e propaga falhas
para `result.json`. Uma inicialização ou verificação incompleta não é sucesso.

Na raiz do clone, com imagem, pacote atual, render node autorizado, Podman
rootless e nginx isolado iniciado conforme as regras anteriores:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -RepeatDaemonCaptures
```

Acrescente `-DenyScreenCastGetAll` para repetir sob recusa. O driver é montado
somente para leitura; a opção exige render node e não altera o cenário padrão.
O orquestrador permanece PowerShell 7; o Python executa somente no container.
Não houve alteração de produção nesta etapa. Este aceite isolado cobre
reutilização da conexão e novas consultas, não propriedades que mudem num
portal naturalmente incompatível, atalhos físicos, Esc, tray ou quit com
janela ainda aberta. GNOME/KDE e a meta de latência permanecem pendentes.

### Quit com sobreposição aberta e reinício

Em 2026-11-01, a branch `test/wayland-daemon-lifecycle` acrescentou o modo
opt-in `-CheckDaemonLifecycle`, que exige `-RepeatDaemonCaptures`. Nenhum
código de produção ou dependência foi alterado nesta etapa.

O driver exige uma sobreposição aberta e verifica sua cor pelo portal antes
de enviar `quit`. O processo deve encerrar com código zero, a janela sumir
e `status` informar ausência do daemon. Depois inicia outro processo, exige
uma nova identidade D-Bus, abre zoom por IPC, verifica o quadro atualizado
e encerra novamente com `quit`. O JSON registra cada asserção; o orquestrador
não aceita resultado de ciclo de vida incompleto.

| Cenário | Evidência local | Resultado |
| --- | --- | --- |
| Teste anterior sem opção nova | `artifacts/wayland/screencast-20261101T130137628-161f4830` | Passou; comportamento anterior preservado |
| Ciclo de vida normal | `artifacts/wayland/screencast-20261101T130141551-ca85f33c` | Quit com janela aberta, status parado, nova conexão e captura após reinício aprovados |
| Ciclo de vida sob recusa `GetAll` | `artifacts/wayland/screencast-20261101T130145567-e269262d` | Mesmas verificações aprovadas com a política D-Bus de recusa |

Com pacote AOT atual, Podman rootless, render node autorizado e nginx isolado
conforme os pré-requisitos anteriores, na raiz do clone:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -RepeatDaemonCaptures -CheckDaemonLifecycle
```

Acrescente `-DenyScreenCastGetAll` para repetir sob recusa. nginx foi mantido
sem portas públicas e com smoke; os containers foram removidos. Não houve
acesso à sessão gráfica, D-Bus ou dados pessoais do host. O pacote testado
foi o AOT já validado da etapa 1. Esta cobertura é Sway isolado/IPC, não
atalhos físicos, Esc, tray, launcher ou aceite nativo GNOME/Windows/macOS.

### Política multi-monitor e erro de Screenshot

Em 2026-11-01, a branch `test/wayland-multi-monitor-policy` validou a política
existente: ScreenCast somente com exatamente um monitor. Cinco casos unitários
cobrem contagens negativas, zero, um, dois e três. Erros D-Bus do fallback
Screenshot agora são convertidos em erro controlado, mantendo cancelamento
e a mensagem de indisponibilidade usada pelo caminho de fallback anterior.

O modo opt-in `-CheckMultiMonitorPolicy` executa o baseline de captura e cria
duas saídas Sway virtuais ativas, verificadas por JSON. A imagem não oferece
Screenshot; o comando `capture` deve terminar com código 1, sem PNG, sem
mensagem de ScreenCast e sem exceção não tratada. Isso comprova a política e
a indisponibilidade segura, não captura bem-sucedida de uma área multi-monitor.

| Cenário | Evidência local | Resultado |
| --- | --- | --- |
| Sem render node | `artifacts/wayland/screencast-20261101T131754378-903d3b19` | Indisponibilidade e mensagem anterior preservadas |
| Um monitor e ciclo de vida | `artifacts/wayland/screencast-20261101T131758088-7c112c2c` | Captura, quit e reinício continuam aprovados |
| Duas saídas, sem Screenshot | `artifacts/wayland/screencast-20261101T131802565-e28d49cf` | `passed=true`, 2 saídas, código 1, nenhum arquivo, erro controlado |

Com pacote AOT atual, imagem existente, Podman rootless, render node autorizado
e nginx isolado conforme os pré-requisitos anteriores, na raiz do clone:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -CheckMultiMonitorPolicy
```

O modo não combina com `-RepeatDaemonCaptures`. O gate passou com format,
build sem avisos/erros, 161 testes, 23 cenários de governança, AOT e `selftest`.
nginx ficou sem portas públicas; os containers foram removidos. Nenhuma
sessão gráfica, D-Bus ou captura pessoal do host foi usada. Permanecem
pendentes captura multi-monitor real com Screenshot disponível, seleção
de monitor, HiDPI, GNOME/KDE e as outras arquiteturas/plataformas.

### Identidade da fonte após fallback e recuperação

Em 2026-11-01, `fix/capture-backend-recovery` separou a captura nativa da seleção
com fallback pela interface `IScreenCapture` existente. O nome é atualizado
somente após captura bem-sucedida: volta a ScreenCast após recuperação e não
muda para Screenshot quando o fallback falha. Cancelamento continua sem fallback;
sessão, consentimento, timeout e fechamento nativos não mudaram.

Três testes focados usam fontes controladas para verificar sucesso → fallback
→ recuperação, falha do fallback e cancelamento. O gate real passou com format,
build sem avisos/erros, 164 testes, 23 cenários de governança, AOT e `selftest`.

| Cenário nativo | Evidência local | Resultado |
| --- | --- | --- |
| Indisponibilidade sem render node | `artifacts/wayland/screencast-20261101T174558524-1a07a163` | Erro controlado e nenhum arquivo |
| Captura e ciclo de vida | `artifacts/wayland/screencast-20261101T174601492-fccadfc0` | Desenho/zoom por IPC, status, quit e reinício passaram |
| Recusa `GetAll` | `artifacts/wayland/screencast-20261101T174606129-0bb81304` | Leituras Get e capturas reais preservadas |

nginx permaneceu isolado sem portas públicas; nenhuma sessão ou D-Bus do host
foi acessado. A sequência de recuperação de identidade é cobertura de lógica
controlada, não de um portal real alternando disponibilidades. A captura real
foi validada no Sway; GNOME/KDE e a matriz de hardware continuam pendentes.

### Investigação de escala fracionária bloqueada

Registro histórico do bloqueio inicial; a validação posterior está na seção seguinte.

Em 2026-11-01, `test/wayland-output-scale` tentou acrescentar testes nas escalas
1, 1,5 e 2. O teste de 1,5 não passou: a saída Sway era logicamente `533×400`,
mas a janela permaneceu `1×1`, sem fullscreen. Evidência:
`artifacts/wayland/screencast-20261101T134318812-3740613a/window-geometry.json`.
Isso é um resultado do cenário isolado, não uma invalidação do aceite humano
anterior no GNOME.

Um probe SDL descartável comparou tamanho inicial zero, tamanho explícito e
fullscreen solicitado após a criação. Candidatos locais de correção passaram
no build, 161 testes, AOT/selftest e E2E X11, mas não na matriz Wayland: houve
janela pequena na segunda sessão e depois falha da captura de inspeção de
conteúdo estático. Não foram classificados como correções aprovadas.

As alterações experimentais de produção e de dano de composição foram
retiradas; `OverlaySession.cs` voltou ao conteúdo da `main`. O AOT experimental
foi preservado em
`artifacts/investigation/fullscreen-experimental-536b8551f28b4f36af044186905856f3`;
não usar esse pacote como implementação validada. Reconstruir a partir da fonte
antes de novos testes. As evidências de falha e os testes de escala permanecem
locais, sem commit/push/PR. O incremento está **bloqueado**, não concluído.
As escalas 1,5 e 2 não receberam aceite deste teste; geometria, composição e
inspeção de quadros no fixture precisam ser investigadas antes de nova entrega.

### HiDPI isolado após separar a inspeção

Em 2026-11-01, a investigação separou dois problemas: criação inicial da janela
Wayland com tamanho zero/fullscreen não aplicado, e inspeção da sobreposição
estática por uma segunda sessão ScreenCast, que pode ficar sem dano no wlroots.
A correção usa dimensões lógicas do display na criação Wayland e solicita
fullscreen depois da criação do renderer apenas se a flag não foi aplicada.
Não usa espera bloqueante `SDL_SyncWindow` nem altera os caminhos X11/Windows/macOS,
consentimento, timeout de captura ou o opt-in de GPU.

A imagem `localhost/pixelpointer-wayland:screencast-v2` fixa
`grim=1.4.0+ds-2+b1` do snapshot Debian existente. A licença Expat/MIT do pacote
exato, incluindo `debian/*`, foi verificada por extração do copyright do `.deb`
assinado, sem executar seus scripts; pacote fornecido pela distribuição Debian
trixie mantida. Os sites upstream/metadados web bloquearam acesso automatizado;
a versão foi obtida do APT do snapshot, não presumida pelo site.
Links nativos de grim: libwayland-client/libffi e pixman (MIT), libpng,
zlib e runtime da distribuição; já presentes na imagem. Versões são inventariadas
em `/opt/native-packages.tsv`. Essa ferramenta é apenas de teste, não integra
o pacote PixelPointer nem sua captura de produção.

O baseline continua exigindo ScreenCast/PipeWire real. O driver inspeciona os
pixels da cena sintética pelo compositor com grim, sem abrir uma segunda
sessão de portal: isso não comprova consentimento, que permanece coberto pelo
baseline. Exige fullscreen e limites lógicos via JSON Sway, tamanho físico
800×600, cor, densidade e ciclo de vida. Falhas antigas não foram reclassificadas.

| Escala | Evidência local | Resultado |
| --- | --- | --- |
| 1 | `artifacts/wayland/screencast-20261101T143030541-59f79fcb` | Desenho, zoom, status, quit e reinício passaram |
| 1,5 | `artifacts/wayland/screencast-20261101T143034841-b65dc44a` | Janela lógica 533×400, pixels 800×600, densidade 1,5; duas sessões e ciclo de vida passaram |
| 2 | `artifacts/wayland/screencast-20261101T143038653-a3d322d0` | Janela lógica 400×300, pixels 800×600, densidade 2; duas sessões e ciclo de vida passaram |

Format, build sem avisos/erros, 161 testes, 23 cenários de governança, AOT e
`selftest` passaram. E2E X11 também passou em
`artifacts/e2e/x11-20261101T143148779-500ce3d3/attempt-01`. nginx permaneceu
isolado sem portas públicas; nenhum socket ou dado gráfico pessoal do host foi usado.

Com imagem v2 construída, pacote AOT atual, render node autorizado e nginx
isolado conforme os pré-requisitos anteriores:

```powershell
pwsh -NoProfile -File tools/scripts/Test-WaylandScreenCast.ps1 -Build -Engine podman -Package artifacts/aot/linux-x64 -RenderNode /dev/dri/renderD128 -RepeatDaemonCaptures -CheckDaemonLifecycle -OutputScale 1.5
```

`OutputScale` aceita 1/1.5/2; valores diferentes de 1 exigem
`RepeatDaemonCaptures`. Capturas do compositor não são prova de atalhos físicos,
caneta/pressão, paridade GPU, multi-monitor ou aceite GNOME/KDE/Windows/macOS.
Esses recortes e o benchmark de latência continuam pendentes; F2/F3 não estão encerradas.

### Primeiro uso opcional e fechamento sem efeitos

Em 2026-11-01, `feat/first-use-setup` acrescentou o comando explícito `setup`.
A política habilita registro no Linux, solicitação de captura no Wayland e
início do daemon somente quando não há instância ativa. A ação inicial é
Fechar. Os comandos existentes e a inicialização do daemon não mudaram.

14 testes cobrem política e limites dos alvos de clique. O gate passou com
format, build sem avisos/erros, 178 testes, 23 cenários de governança, AOT e
`selftest`. O teste opt-in `-CheckSetupWindow` verificou a região da janela
não vazia e o fechamento por compositor sem criar/alterar arquivos do app
nem mudar o estado do daemon, com e sem daemon ativo. Evidência:
`artifacts/wayland/screencast-20261101T182402112-1a823db2`.
nginx ficou isolado sem portas públicas; nenhum dado gráfico pessoal do host foi usado.

O teste não clicou em solicitar captura nem concedeu permissões automaticamente.
Consentimento real no GNOME, acionamento físico dos controles e fluxo nativo
Windows/macOS continuam pendentes. “Solicitação finalizada” não é comprovação
de permissão concedida. Os detalhes do diagnóstico existente ficam no terminal.

Gerar uma **nova preview após integrar o PR desta entrega na main com CI verde**,
antes do aceite manual de primeiro uso. Não é necessário esperar toda a F2;
a preview existente não contém `setup`. Após gerar e aprovar o Environment
`release`, testar no GNOME/Wayland e Windows: abrir/fechar sem ações, diagnóstico,
registro explícito quando aplicável, cancelamento/consentimento voluntário e
início do daemon por escolha. Não desmarcar Pre-release nem promover a estável
antes de validar os recortes afetados.

### Resultado local em 2026-10-30

Etapas 1 a 6 implementadas para o primeiro alvo. A etapa 7 permanece expansão
condicionada, não cobertura declarada. Execução por Podman rootless, sem acesso
à sessão gráfica real do solicitante e sem mudanças nos workflows gerenciados.

Imagem final: `16ddca00a969716ee1db935ea42c0a9200a82051b49a9718345ca0ad6ca513cb`.
SHA-256 do executável testado:
`eae12d3d269670a7da1914ea028fb7cc478f9aa8e0416b1849134b2338f1eb31`.
Os manifests registram também os hashes das bibliotecas e demais arquivos do pacote.

| Verificação | Resultado e diretório local em `artifacts/e2e/` |
| --- | --- |
| Estabilidade, dez desktops limpos | 10/10; `x11-20261030T175407362-09a7e896/attempt-01` até `attempt-10` |
| Morte deliberada do daemon | Código 1, motivo `Daemon interrompido: -9`, JUnit reprovado e vídeo preservado; `x11-20261030T175704158-6db4df5b/attempt-01` |
| Sem compositor | Passou, com aviso e PNG opaco; `x11-20261030T175751758-897e6ec5/attempt-01` |
| Sem host StatusNotifier | Passou, com limitação explícita e controle preservado; `x11-20261030T175842166-cb50e0dc/attempt-01` |
| Integridade das evidências | 13 diretórios, 750 frames JPEG decodificados; resolução, contagem, JUnit, JSON e cobertura da ação final conferidos |
| Coletor | 3 testes passaram; truncamento, vídeo vazio, contagens e preservação de reprovação |
| Qualidade .NET | Formatação e build sem avisos/erros, 59 testes passaram, Native AOT e selftest passaram |

O E2E revelou um defeito real: pixels BGRX vindos de XGetImage podiam conter zero
no byte não utilizado, preservado pelo caminho SKImage como alfa zero. O zoom
ficava preto/transparente apesar de a captura direta parecer correta. A cópia
agora normaliza esse byte para 255. Um teste Skia reproduziu a falha antes da
correção e passou depois, assim como zoom e desenho transparente no E2E.

O ambiente usa visual TrueColor de 32 bits selecionado explicitamente pelo hint
público SDL no cenário com compositor, sem GLX. O painel e o launcher são
sintéticos e recebem cliques XTEST reais; isso não comprova integração GNOME nem
seleção automática de visual. Não há servidor de visualização remota ou Playwright:
o produto é SDL nativo; nginx permanece isolado com smoke HTTP e sem portas públicas.
Revisão humana dos vídeos/referências e demais ambientes continuam pendentes.

### Aceite manual do desenho transparente em GNOME X11

Em 2026-10-30, o solicitante executou o pacote na própria sessão e confirmou:
“Teste realizado com sucesso”, fornecendo um PNG de desenho como evidência.
O teste foi conduzido pelo usuário; a automação não acessou sua sessão gráfica.

- Ambiente informado por `probe`: Ubuntu 24.04.5 LTS x64, GNOME/X11, compositor
  presente, dois monitores de 1920×1080 e escala 1.
- Daemon iniciado sem `SDL_VIDEO_X11_WINDOW_VISUALID`, `SDL_VIDEODRIVER` e
  `SDL_RENDER_DRIVER`; o SDL selecionou X11 e renderer OpenGL automaticamente.
- Confirmação humana: desenho transparente funcionando, seguida do teste de
  exportação com alfa solicitado nesta conversa. Os logs também registraram zoom
  com captura de 3840×1080 e dois salvamentos nesse modo.
- Confirmação complementar do solicitante, em 2026-10-30: “teste com
  CapsLock/NumLock funcionando, e testes com uso em outros monitores também ok,
  autorizo considerar com concluido”. Os testes de modificadores e uso nos outros
  monitores ficam concluídos por aceite humano explícito na configuração informada.
- Evidência visual complementar em 2026-10-30: o solicitante informou teste no
  monitor 1 e anexou três prints nesta conversa. O primeiro mostra o ícone próprio
  no painel GNOME e o menu aberto com Zoom, Desenhar e Sair; os outros mostram
  anotações vermelhas, incluindo a identificação manuscrita do monitor 1.
  A visibilidade do ícone e a abertura do menu ficam verificadas. Os anexos
  permanecem como evidência da conversa, sem cópia para o repositório. Seu fundo
  visualizado não comprova nem refuta alfa; a análise do PNG original abaixo é
  a evidência objetiva de transparência do arquivo.
- Aceite de encerramento em 2026-10-30: o solicitante confirmou “Encerramento por
  Sair funciona” e anexou um print mostrando retorno ao prompt após as sessões de
  desenho e consulta `status` com “Daemon não está em execução”. A ação por Sair
  é o relato humano; retorno ao terminal e status são a evidência visual do
  resultado. O fluxo tray → Sair fica concluído nesse GNOME/X11. O print não
  comprova código de saída, inspeção individual de recursos nem as duas variantes
  com/sem sobreposição aberta; também não comprova início pelo launcher.
- PNG de desenho inspecionado separadamente em container: RGBA, 1920×1080,
  2.054.243 pixels com alfa zero, 13.881 com alfa 255 e 5.476 com alfa intermediário.
  Isso comprova fundo transparente e conteúdo opaco/parcialmente transparente no
  arquivo; a observação do desktop ao vivo é o relato humano, não inferência do PNG.
- SHA-256 da evidência:
  `564c0d4658dafc50d081ecd63c35bd482e1433b4b5d4da8b65595926aa72344d`.
  O arquivo pessoal permanece fora do repositório, sem cópia ou publicação;
  somente o arquivo explicitamente indicado foi montado para análise, em leitura.

Esse aceite resolve a ressalva de seleção automática do visual para esse ambiente
e confirma a exportação transparente, CapsLock/NumLock e uso nos dois monitores
testados, com evidência complementar do monitor 1 e do ícone/menu da tray GNOME.
O encerramento por Sair também possui aceite humano e status sem daemon.
Não comprova o acionamento das opções Zoom/Desenhar pela tray, launcher,
conflitos físicos com outros apps, escala fracionária, outras topologias/GPUs,
Wayland ou a matriz GNOME completa.
Também não substitui a revisão humana pendente dos vídeos da suíte automatizada.

### Aceite manual no Ubuntu 26.04/GNOME Wayland

Em 2026-10-30, após a instalação da release `v0.1.2` orientada nesta conversa,
o solicitante informou: “Testes realizado com wayland ubuntu 26.04 tudo
funcionando veja os prints”. Esse é o aceite humano dos fluxos exercitados,
não uma execução automatizada nem uma confirmação individual de toda a matriz.

| Recorte | Evidência e resultado |
| --- | --- |
| Ambiente | Ubuntu 26.04, GNOME/Wayland, conforme relato e contexto da conversa; pacote de referência linux-x64 v0.1.2. Os prints não mostram `--version`, `probe`, escala nem topologia de monitores |
| Desenho e cores | Traços vermelhos e azuis sobre a imagem da área de trabalho; um print posterior mostra um novo traço azul |
| Quadros e retorno | Fundos branco e preto conservam os traços; outro print mostra retorno à imagem da área de trabalho com as anotações |
| Identidade desktop | Ícone próprio PixelPointer e nome visíveis no launcher; ícone no painel e menu com Zoom, Desenhar e Sair visíveis |
| Encerramento visual da anotação | Prints sem traços mostram a área de trabalho novamente; não comprovam encerramento do daemon nem qual comando foi usado |
| Resultado humano | Solicitante confirmou funcionamento geral na sessão testada; zoom, atalhos, permissões e ações do menu não têm demonstração individual nos anexos |

Os seis anexos permanecem como evidência desta conversa, sem cópia, hash,
publicação ou inspeção de arquivos pessoais. A automação não acessou a sessão
gráfica do usuário. O instalador publicado da `v0.1.2` foi conferido separadamente
nesta conversa: exige e copia `pixelpointer.svg`; os prints corroboram a identidade
visual no desktop, mas não substituem os testes de pacote/instalador.

**Aceite complementar do desenho transparente em 2026-10-30:** o solicitante
forneceu dois novos prints nesta conversa, identificando-os como “duas evidencias
para desenho comtransparencia”. O primeiro mostra um traço vermelho sobre a
imagem de um vídeo no navegador; o segundo mostra uma anotação circular vermelha
sobre outro conteúdo do vídeo. O conteúdo do navegador permanece visível sob a
área de desenho. O desenho transparente fica concluído por aceite humano nessa
sessão Ubuntu 26.04/GNOME Wayland, corroborado visualmente pela sobreposição.
São oito anexos ao todo nesse registro; todos permanecem somente na conversa.
Não houve análise de arquivo PNG original nem gravação/automação da sessão.
Como as anotações e os conteúdos diferem entre os dois prints, eles não comprovam
movimento contínuo do fundo sob um mesmo traço persistente; essa verificação
objetiva permanece pendente, assim como a análise de alfa do PNG exportado.

Limites: imagem estática não comprova transparência ao vivo, alfa de PNG,
latência, caminho Wayland nativo versus XWayland, ScreenCast/PipeWire ou qual
portal foi utilizado. Abertura sem terminal, conflitos de atalhos, CapsLock/NumLock
nesse Wayland, falhas/recusa de permissões, outros monitores, HiDPI, caneta,
outros compositores e Windows/macOS não foram detalhados nesse reteste.
Continuam pendentes o E2E Wayland isolado por compositor e a revisão humana dos
vídeos automatizados X11. O aceite não conclui as fases F2/F3 inteiras.

### Validação ScreenCast + PipeWire em Wayland isolado

`tools/scripts/Test-WaylandScreenCast.ps1` executa o pacote em container com
sway headless, PipeWire, WirePlumber, xdg-desktop-portal e
xdg-desktop-portal-wlr (snapshot Debian fixado), sem rede e sem sessão, D-Bus ou
diretórios do host. O seletor do xdg-desktop-portal-wlr está configurado para a
única saída virtual; não há diálogo GNOME nesse ambiente.

| Modo | Verificação | Resultado em 2026-10-31 |
| --- | --- | --- |
| Padrão, sem GPU | O xdg-desktop-portal-wlr 0.7 exige DMA-BUF e recusa o renderer pixman ([#289](https://github.com/emersion/xdg-desktop-portal-wlr/issues/289)); `capture` deve falhar com código 1, aviso de retorno ao Screenshot e erro claro, sem exceção não tratada nem arquivo | Aprovado |
| `-RenderNode /dev/dri/renderD128` (Podman rootless, `keep-id`) | Duas capturas 800x600 por ScreenCast, cor exata do fundo, restore token 0600 reutilizado e quadro novo após mudar o fundo | Aprovado: portal 7–8 ms, primeiro quadro 19–26 ms, 36 ms no total |

O modo com GPU expõe somente o nó de renderização, opcionalmente. Os tempos são
do ambiente virtual sem diálogo; não substituem a medição no GNOME real, onde a
meta continua < 150 ms. Em wlroots o quadro só chega após dano na tela; sem dano
a captura volta ao Screenshot após 2 s.

## 9. Referências técnicas

- [Scripts e lockfiles no uv](https://docs.astral.sh/uv/guides/scripts/).
- [Servidor virtual Xvfb](https://www.x.org/releases/current/doc/man/man1/Xvfb.1.xhtml).
- [Entrada emulada libei/EIS](https://libinput.pages.freedesktop.org/libei/).
- [Roteiro manual da F0](f0-relatorio.md#6-roteiro-de-teste-manual-por-plataforma).
- [Roteiro manual X11](uso.md#roteiro-manual-de-aceite-no-x11).
