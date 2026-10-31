# Plano de E2E gráficos isolados com uv e evidência em vídeo

**Português (Brasil)** · [English](en/plano-e2e-grafico-uv.md) · [Español](es/plano-e2e-grafico-uv.md)

| Campo | Estado |
| --- | --- |
| Data | 2026-10-30 |
| Autorização humana | Solicitante, nesta conversa: “Eu autorizo os E2E gráficos isolados nas regras do projeto” |
| Decisão autorizada | Permitir uma suíte gráfica automatizada separada e isolada |
| Estado de execução | Primeiro incremento X11 implementado e executado; estabilidade 10/10; revisão humana dos vídeos pendente. Aceite manual adicional Ubuntu 26.04/GNOME Wayland registrado abaixo; não é E2E automatizado |
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
| Wayland | Aceite manual Ubuntu 26.04/GNOME após instalação da v0.1.2: relato de funcionamento dos fluxos exercitados e prints de desenho, quadros, launcher e tray. E2E isolado por compositor, libei/EIS e portais reais continuam pendentes; não substituir consentimento por mock nem usar XTEST via XWayland como prova do caminho nativo |
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

## 9. Referências técnicas

- [Scripts e lockfiles no uv](https://docs.astral.sh/uv/guides/scripts/).
- [Servidor virtual Xvfb](https://www.x.org/releases/current/doc/man/man1/Xvfb.1.xhtml).
- [Entrada emulada libei/EIS](https://libinput.pages.freedesktop.org/libei/).
- [Roteiro manual da F0](f0-relatorio.md#6-roteiro-de-teste-manual-por-plataforma).
- [Roteiro manual X11](uso.md#roteiro-manual-de-aceite-no-x11).
