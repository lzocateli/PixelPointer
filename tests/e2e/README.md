# E2E grafico X11 isolado

Implementacao do primeiro alvo do [plano](../../docs/plano-e2e-grafico-uv.md).
Exercita o pacote Native AOT por entrada XTEST, pixels do desktop, IPC publico e
um painel StatusNotifier sintetico. Nao acessa a sessao grafica do desenvolvedor.

## Executar

Execute na raiz do repositorio, em Linux x64, com PowerShell 7 e Podman ou Docker.
Builds precisam de acesso ao registry, NuGet, PyPI e snapshot Debian. O teste
grafico executa sem acesso externo. Docker possui interface prevista, mas esta
entrega foi verificada somente com Podman rootless.

```powershell
pwsh -NoProfile -File tools/scripts/Publish-Aot.ps1 -Rid linux-x64 -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -Build -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -Repeat 10 -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -NoCompositor -Engine podman
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -NoTray -Engine podman
```

`Publish-Aot.ps1` produz `artifacts/package/linux-x64`. `-Package` aceita somente
um diretorio dentro de `artifacts`, sem links simbolicos. `-Build` constroi a
imagem local; sem essa opcao, a imagem deve existir. Cada chamada fixa o ID da
imagem inspecionada para todas as tentativas, sem reconstrucoes intermediarias.
`--help` nao inicia containers. Nao execute o runner diretamente no host.

O resultado normal e codigo zero. Falhas, ausencia de evidencias ou timeout
retornam codigo diferente de zero; a primeira tentativa reprovada interrompe a
sequencia. Para comprovar a deteccao de falha:

```powershell
pwsh -NoProfile -File tools/scripts/Test-GraphicalE2E.ps1 -InjectFailure -Engine podman
```

Esse comando **deve falhar** com codigo 1: o runner mata somente o daemon filho e
exige sua disponibilidade na proxima verificacao. O relatorio deve registrar
`Daemon interrompido: -9`, nunca converter essa falha em sucesso do cenario.

## Cobertura e referencias

A matriz funcional pertence ao [plano](../../docs/plano-e2e-grafico-uv.md#5-matriz-de-cenários-das-funcionalidades-atuais).
O runner verifica foco externo, atalhos, modificadores, conflito real de grab,
zoom, cores, espessura, edicao, quadros, alfa, salvamento e erro visivel,
tray, reinicio, quit com janela aberta e instalacao/abertura/remocao do launcher.
O launcher interpreta o arquivo desktop instalado, mas aceita exclusivamente
`/package/pixelpointer daemon`, sem shell e com `Terminal=false`.

Referencias sao calculadas por regioes e cores explicitas no runner, nao por
baselines atualizadas automaticamente. Exemplos: cena ciano RGB `(0,170,204)`,
larguras de zoom `280/350/438` com tolerancia de 4 pixels, cores de anotacao com
tolerancia de 8 canais e area minima, fundo animado alterado com traco persistente,
PNG transparente com compositor e opaco sem compositor. Os PNGs sao decodificados.

Geometria fixa: 800x600, escala 1, renderizacao SDL por software, locale `C.UTF-8`,
UTC e compositor `xcompmgr -n`. A imagem nao tem GLX/Mesa. No caso com compositor,
o runner seleciona explicitamente o visual TrueColor de 32 bits pelo hint publico
`SDL_VIDEO_X11_WINDOW_VISUALID`; sem compositor, usa o visual padrao de 24 bits.
Isso comprova o desenho transparente nessa configuracao, **nao** a selecao
automatica de visual nem paridade com GNOME, GPU ou um desktop completo.

## Evidencias e retencao

Resultados ficam em `artifacts/e2e/x11-<UTC>-<id>/attempt-NN`, sem sobrescrita:

| Arquivo | Conteudo |
| --- | --- |
| `desktop.avi` | Captura XGetImage real do desktop, MJPEG a 5 FPS, sem audio |
| `*.png` | Screenshots, captura CLI e exportacoes reais de Ctrl+S |
| `timeline.jsonl` | Acoes e verificacoes com tempo monotonicamente crescente |
| `result.json`, `junit.xml` | Resultado, duracao e motivo de falha |
| `manifest.json` | SHA-256 de cada arquivo do pacote, ID da imagem, ambiente e video |
| `native-components.tsv`, `native-packages.tsv` | Fontes compiladas e pacotes Debian exatos |
| `native-links.txt`, `python-packages.json` | Links nativos resolvidos e dependencias Python instaladas |

O gravador inicia antes da primeira acao do produto. Cada screenshot espera um
frame posterior antes de prosseguir; o encerramento tambem exige frame posterior
a ultima acao. Todos os JPEGs sao decodificados, com resolucao obrigatoria e
tolerancia de duracao de 0,4 segundo. Falha de gravacao reprova o resultado.
Video nao substitui as assercoes objetivas.

Nao ha publicacao, rotacao nem exclusao automatica dos artefatos. Preserve
execucoes reprovadas ate concluir a analise; preserve a sequencia de estabilidade
e a falha controlada ate revisao humana. A remocao posterior e manual, limitada
ao identificador explicitamente escolhido pelo responsavel. Nao use prune global.

## Isolamento e ameacas

| Superficie | Controle e risco residual |
| --- | --- |
| Captura e entrada | Apenas Xvfb privado; nenhum display, D-Bus, home ou dispositivo do host montado |
| Privilegios | UID 1000, capabilities removidas, no-new-privileges, filesystem somente leitura e tmpfs privado |
| Rede | Rede interna, sem portas publicadas; nginx separado e smoke HTTP 404; nao existem auxiliares HTTP diretos |
| Pacote | Mount somente leitura e caminhos normalizados sem symlinks; executar apenas pacote local confiavel |
| Escrita | Volume exclusivo de evidencias e temporarios descartaveis; nenhum secret ou perfil real |
| Processos | Argumentos conhecidos, sem shell no runner/launcher, encerramento apenas dos filhos e recursos identificados |
| Recursos | Runner: 2 CPUs, 2 GiB, 128 processos e tmpfs 512 MiB; nginx: 128 MiB e 32 processos |
| Diagnostico | Apenas tokens conhecidos e mensagens de teste; nao coletar logs integrais nem ambiente completo |

X11 foi iniciado com `-ac`, aceitavel apenas dentro desse container sem socket ou
porta expostos. A rede interna e o sandbox nao tornam seguro executar um pacote
hostil contra o kernel do host. Imagem e pacote devem ser confiaveis. O volume de
evidencias e somente um canal de saida do teste, nao um local para dados pessoais.
Nenhuma permissao de portal/TCC e simulada como consentimento real.

O prazo cooperativo e 120 segundos; SIGALRM em 150 segundos levanta erro para
finalizar evidencias; o orquestrador mata o container em 180 segundos. SIGKILL ou
falha do engine podem impedir a finalizacao do AVI; isso nao e aprovacao.

## Diagnostico e recuperacao

1. Leia `result.json` e a ultima verificacao em `timeline.jsonl`; confronte o PNG
   de falha e o video. Falha de infraestrutura tambem reprova o cenario.
2. Se o build falhar, confira acesso ao digest/snapshot e o gate de links nativos.
   Nao substitua versoes ou relaxe licencas automaticamente para obter sucesso.
3. Se a exportacao falhar ou ocorrer timeout externo, o script informa o volume
   preservado. Recupere-o em container auxiliar de mesma imagem, somente leitura,
   com `podman create --name <auxiliar-unico> --network none --read-only --volume
   <volume-informado>:/evidence:ro --entrypoint python
   localhost/pixelpointer-e2e:x11-v1 --version`, e use `podman cp
   <auxiliar-unico>:/evidence/results/. <diretorio-novo>`. Remova somente esse
   auxiliar e, apos conferir a copia, o volume explicitamente identificado.
4. Se faltarem relatorio ou frames, mantenha o resultado reprovado. Uma nova
   execucao recebe outro identificador e nao apaga a primeira.
5. Escale diferencas entre desktop sintetico e real para o roteiro manual e o
   responsavel pelo backend; nao monte a sessao do host para contornar o limite.

## Dependencias e licencas

Ferramentas exclusivas de teste, nao adicionadas ao pacote distribuido. Verificacao
local em 2026-10-30, com avisos dos artefatos exatos, metadados PyPI e snapshot
Debian. Python e manifesto/lock estao versionados; `uv sync --locked` ocorre no
build e o runtime usa `uv run --locked --no-sync`, offline e sem downloads.

| Componente | Versao fixada | Licenca escolhida e fonte |
| --- | --- | --- |
| Python | 3.13.17 | PSF-2.0; imagem oficial por digest no Containerfile |
| uv | 0.13.6 | MIT, alternativa Apache-2.0; [release PyPI](https://pypi.org/project/uv/0.13.6/) |
| Pillow | 13.0.0 | MIT-CMU e avisos transitivos do wheel; [release PyPI](https://pypi.org/project/pillow/13.0.0/) |
| dbus-fast | 5.2.0 | MIT; [release PyPI](https://pypi.org/project/dbus-fast/5.2.0/) |
| Xvfb | 2:21.1.16-1.3+deb13u4 | MIT/X11 e avisos permissivos de `COPYING`; fonte Debian xorg-server |
| D-Bus | 1.16.2-2 | AFL-2.1, **nao** a alternativa GPL; ferramentas GPL desabilitadas |
| twm | 1:1.0.10-1+b1 | MIT/X11/Open Group, avisos em `/opt/twm/copyright` |
| xcompmgr | 1.1.8-1 | XFree86 permissiva; copyright Debian instalado |
| nginx | 1.26.3-3+deb13u10 | BSD-2-Clause, `LICENSE` da fonte compilada |

Fontes Debian: [main](https://snapshot.debian.org/archive/debian/20261029T000000Z/)
e [security](https://snapshot.debian.org/archive/debian-security/20261029T000000Z/).
Releases PyPI consultadas: uv em 2026-10-27, Pillow em 2026-10-16 e dbus-fast em
2026-10-02; Xorg/nginx incluem as revisoes de seguranca fixadas acima. Atualizar
exige nova verificacao de manutencao, avisos, lock, build e matriz.

Os wheels nao acrescentam dependencias Python transitivas. O wheel Pillow inclui
bibliotecas nativas e seus textos no `pillow-13.0.0.dist-info/licenses/LICENSE`:
AOM/dav1d/libavif (BSD), Brotli/HarfBuzz (MIT), bzip2, libjpeg (IJG), lcms2 (MIT),
liblzma (0BSD), libpng (libpng), libtiff (libtiff), libwebp/libyuv (BSD),
OpenJPEG (BSD), libraqm (MIT), Xau/XCB/Xdmcp (MIT/X11), zlib e zstd (BSD).
Para FreeType escolhe-se FTL, nao GPL; para zstd, BSD, nao GPL. Os avisos completos
do wheel prevalecem sobre este resumo; nao foram incluidos FFmpeg nem FriBidi.

Xvfb e D-Bus sao compilados sem libsystemd, libaudit, libapparmor, libunwind e GLX;
nginx sem auth_basic, gzip, rewrite e PCRE. O gate `ldd` recusa links ausentes e
familias opcionais incompatíveis como nettle, libelf e libcrypt. Bibliotecas X11,
pixman, expat, fontconfig/FreeType e OpenSSL usam alternativas permissivas;
versoes transitivas sao fixadas pelo snapshot e inventariadas a cada execucao.
`/opt/native-notices` guarda avisos das fontes compiladas. O inventario Debian
tambem lista ferramentas e componentes preexistentes do sistema base; isso nao
significa que toda a distribuicao ou toolchain tenha licenca MIT. A imagem de
teste nao e um novo artefato de distribuicao do PixelPointer.

## Limites de aceite

A suite automatizada foi executada somente em Linux x64/X11 virtual com
twm/painel sintetico. O solicitante confirmou separadamente desenho transparente,
selecao automatica de visual, zoom, exportacao com alfa, CapsLock/NumLock e uso
nos dois monitores 1920x1080 em escala 1 no GNOME/X11 real, autorizando concluir
esses testes;
ambiente, hash do PNG e limites estao no [registro de aceite manual](../../docs/plano-e2e-grafico-uv.md#aceite-manual-do-desenho-transparente-em-gnome-x11).
A matriz GNOME completa, Wayland/portais, arm64, Windows/macOS, outras GPUs,
caneta, escala fracionaria e outras topologias de monitores continuam nao
verificados. A revisao humana dos primeiros videos e referencias da suite
permanece pendente; resultados automatizados nao concedem esse aceite.
