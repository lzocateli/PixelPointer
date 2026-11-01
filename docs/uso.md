# Guia de uso

**Português (Brasil)** · [English](en/usage.md) · [Español](es/uso.md)

**Versão funcional de referência:** `v0.1.2`, primeira versão funcional aceita
como estável pelo mantenedor em 2026-10-30 no Ubuntu 24.04.5/GNOME X11 e Ubuntu
26.04/GNOME Wayland. O aceite é manual e limitado aos fluxos exercitados;
Windows/macOS, outros compositores e a matriz completa permanecem pendentes.
Consulte o [registro de evidências](plano-e2e-grafico-uv.md). O relatório da F0
é histórico; esse aceite não altera os limites dos backends descritos abaixo.

## Primeiro uso opcional

Execute `pixelpointer setup` numa sessão gráfica para abrir a janela de
primeiro uso. É um fluxo explícito: não aparece automaticamente ao iniciar
o daemon e não substitui os comandos existentes. Abrir, fechar ou pressionar
Esc não registra o aplicativo, não solicita captura e não inicia o daemon.
“Fechar” é a ação inicialmente selecionada.

- “Verificar ambiente” executa o diagnóstico existente; a saída fica no terminal.
- “Registrar aplicativo no desktop” é oferecido somente no Linux e preserva conflitos.
- “Solicitar captura de tela” é oferecido somente no Linux/Wayland; abre o fluxo
   `grant` existente, exigindo confirmação na janela e no portal. “Solicitação
   finalizada” não significa que a permissão foi concedida; confira o resultado
   do comando e a decisão do desktop.
- “Iniciar daemon” é oferecido apenas se não houver daemon ativo. A janela
   fecha e o daemon segue em primeiro plano, como no comando `daemon` atual.

As ações são escolhidas por clique ou navegação Tab/setas e Enter. O fluxo não
altera PATH, perfis, início automático ou configurações, nem grava um marcador
de primeiro uso. Registro e permissões só ocorrem após sua escolha explícita.
Se já há daemon, fechar `setup` não o encerra. Para diagnosticar, prefira
executar a partir de um terminal; os detalhes e erros ficam nessa saída.

## Comandos

| Comando | Função |
| --- | --- |
| `pixelpointer daemon` | Fica em execução, registra os atalhos globais (Ctrl+1 zoom, Ctrl+2 desenho) e o canal local de comandos; Ctrl+C ou SIGTERM encerra |
| `pixelpointer zoom` | Abre o zoom agora; com o `daemon` em execução, envia o comando a ele |
| `pixelpointer draw` | Abre o desenho sem zoom; mesma regra do `zoom` |
| `pixelpointer status` | Informa se o daemon está em execução, sem abrir uma sobreposição |
| `pixelpointer quit` | Encerra o daemon e uma sobreposição ativa |
| `pixelpointer overlay [--image arq.png] [--mode zoom\|draw]` | Abre a sobreposição sobre uma captura nova ou sobre uma imagem |
| `pixelpointer capture [--out arq.png]` | Captura a tela e informa a latência |
| `pixelpointer probe` | Diagnóstico: SO, sessão, monitores, versões dos portais |
| `pixelpointer install-desktop-file` | Linux: instala a entrada visível do menu e o ícone próprio com o app id `io.github.lzocateli.PixelPointer` |
| `pixelpointer uninstall-desktop-file` | Linux: remove os recursos por usuário somente se forem idênticos aos esperados; preserva arquivos modificados |
| `pixelpointer grant` | GNOME/Wayland: pede a permissão de captura com uma janela focada |
| `pixelpointer setup` | Primeiro uso opcional com ações explícitas de diagnóstico, registro, solicitação de captura e início do daemon |
| `pixelpointer selftest` | Confere as bibliotecas nativas sem tela (usado pelos testes de pacote) |
| `pixelpointer --version` / `--help` | Versão e ajuda |

Teclas na sobreposição: roda do mouse ou setas para o zoom, clique e arraste
para desenhar, R/G/B/Y/O/P para cores, Ctrl+roda para a espessura, Ctrl+Z para
desfazer, E para apagar, W/K para quadro branco/preto, Ctrl+S para salvar PNG
na pasta de capturas do usuário, Esc ou botão direito para sair.

Formas, como no ZoomIt: segure a tecla ao começar a arrastar — Shift desenha
linha reta, Ctrl retângulo, Ctrl+Shift seta e Tab elipse. A forma acompanha o
mouse até soltar o botão e usa a cor e a espessura atuais.

## Onde o Ctrl+S salva

| Sistema | Destino |
| --- | --- |
| Windows | Pasta especial **Imagens** do usuário + `Screenshots`, respeitando redirecionamentos, inclusive OneDrive |
| macOS | Preferência `location` de `com.apple.screencapture`; quando ausente, Área de Trabalho do usuário |
| Linux (X11 e Wayland) | Pasta `PICTURES` de XDG User Directories + `Screenshots` |

No Linux, a configuração é lida de `$XDG_CONFIG_HOME/user-dirs.dirs` ou,
na ausência de `XDG_CONFIG_HOME`, de `~/.config/user-dirs.dirs`. Por exemplo,
`XDG_PICTURES_DIR="$HOME/Imagens"` resulta em `~/Imagens/Screenshots`.
Se a pasta de imagens estiver ausente ou desabilitada, configure-a nas
preferências do desktop ou com a ferramenta XDG da distribuição antes de salvar.
Não há um destino universal compartilhado com outras ferramentas de captura.

A configuração é consultada a cada Ctrl+S. O aplicativo cria o destino quando
necessário, usa nomes exclusivos e não sobrescreve capturas anteriores. No Linux
e no macOS, os novos PNGs permitem leitura e escrita apenas ao usuário (0600).
O caminho final ou o erro aparece na própria sobreposição, sem bloquear o daemon;
também é informado no terminal quando houver. Em caso de erro, não há salvamento
silencioso no diretório atual nem em uma pasta alternativa.

O PNG contém a cena e as anotações da sobreposição, sem o cursor ou a mensagem de
salvamento. No desenho transparente X11, o fundo do PNG também é transparente:
ele exporta as anotações, sem recapturar o desktop. Para salvar a tela congelada
com anotações, use o zoom; W/K exportam o quadro branco/preto explicitamente.

## Configurações

O arquivo `settings.json` é opcional; sem ele, valem os padrões abaixo. O
aplicativo só lê o arquivo, a cada sobreposição, e nunca o cria nem o regrava.
`pixelpointer probe` mostra o caminho efetivo e se o arquivo foi carregado.

| Sistema | Caminho |
| --- | --- |
| Windows | `%APPDATA%\PixelPointer\settings.json` |
| macOS | `~/Library/Application Support/PixelPointer/settings.json` |
| Linux | `$XDG_CONFIG_HOME/pixelpointer/settings.json` ou `~/.config/pixelpointer/settings.json` |

```json
{
  "version": 1,
  "pen": { "color": "red", "width": 6 },
  "zoom": { "initial": 2 }
}
```

| Campo | Valores | Padrão |
| --- | --- | --- |
| `version` | `1` | `1` |
| `pen.color` | `red`, `green`, `blue`, `yellow`, `orange`, `pink` | `red` |
| `pen.width` | 1 a 60 | 6 |
| `zoom.initial` | 1 a 16, ampliação ao abrir o zoom | 2 |
| `rendering.gpu` | `true` ou `false`; desenha com OpenGL | `false` |

Campos omitidos usam o padrão; comentários `//` e vírgulas finais são aceitos.
Campo desconhecido, valor fora da faixa, JSON malformado, arquivo acima de
64 KiB ou fora de UTF-8 invalidam o arquivo inteiro: a sobreposição abre com os
padrões e o motivo aparece no terminal e no `probe`. As teclas continuam alterando
cor e espessura durante a sessão sem gravar no arquivo.

`rendering.gpu` é experimental e está desligado por padrão. Quando ligado, a
sobreposição tenta OpenGL e volta à renderização em CPU, com aviso no terminal,
se a janela ou o contexto OpenGL não puderem ser criados. O desenho transparente
X11 e `SDL_RENDER_DRIVER=software` continuam em CPU. A linha `quadros` do
terminal informa o caminho usado.

## Permissões por sistema

### Linux com Wayland (GNOME, KDE, Hyprland)

O Wayland não permite que um programa veja a tela ou o teclado de outros; o
PixelPointer usa os portais XDG do desktop.
O desenho em Wayland usa a captura autorizada pelo portal e permanece
congelado; transparência sobre conteúdo ao vivo não está implementada nesse
backend.

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

**Captura por ScreenCast.** Fora do GNOME, com um único monitor, cada Ctrl+1/Ctrl+2 abre uma
sessão do portal ScreenCast, lê um quadro pelo PipeWire (`libpipewire-0.3` do
sistema) e encerra a sessão; o indicador de compartilhamento de tela aparece
apenas durante essa captura. Na primeira vez o desktop pede para escolher e
autorizar o monitor. O *restore token* devolvido pelo portal fica em
`$XDG_STATE_HOME/pixelpointer/screencast-restore-token` (ou
`~/.local/state/...`), com permissão 0600, e evita repetir o diálogo; apague o
arquivo para revogar. Cancelar o diálogo cancela a sobreposição. Com mais de um
monitor, sem PipeWire ou sem o portal ScreenCast, a captura usa o portal
Screenshot. Em compositores wlroots (Sway, Hyprland), o quadro só chega após
alguma mudança na tela; sem ela, a captura usa o Screenshot após 2 s. O terminal
informa o caminho usado e o tempo do portal e do primeiro quadro.

No GNOME (`XDG_CURRENT_DESKTOP` com `GNOME`), a captura usa sempre o portal
Screenshot, autorizado uma vez por `pixelpointer grant`: no GNOME 46 o indicador
de compartilhamento ficava preso após capturas ScreenCast curtas, mesmo sem
sessão ativa. A latência do Screenshot é maior.

Com vários monitores, ScreenCast não é usado silenciosamente para capturar
apenas um deles: o caminho selecionado é Screenshot. Erros D-Bus desse portal
são apresentados como erro de captura controlado, sem exceção não tratada.
Se nenhum caminho estiver disponível para esse cenário, a captura falha;
isso não equivale a suporte multi-monitor validado em todos os desktops.

O nome da fonte acompanha a última captura concluída: se o ScreenCast voltar
a funcionar depois de um fallback, o diagnóstico volta a indicar ScreenCast.
Falha do fallback não informa uma fonte como concluída e cancelamento não
aciona outra captura para contornar a recusa.

**Diagnóstico por etapas.** Os totais `portal` e `primeiro quadro` são
preservados: `portal` cobre criação da sessão, seleção/início do portal e
persistência da autorização; `primeiro quadro` inclui abertura do remoto
PipeWire e leitura/conversão. A mensagem também detalha as durações:

| Campo | Etapa |
| --- | --- |
| `sessao` | Criar a sessão ScreenCast |
| `fontes` | Selecionar fontes de captura |
| `inicio` | Iniciar a sessão; inclui espera pelo consentimento se o portal mostrar diálogo |
| `persistencia` | Tentar salvar a autorização para a próxima captura |
| `remoto` | Abrir a conexão PipeWire |
| `leitura` | Obter e converter o primeiro quadro |
| `restore solicitado` | `sim`/`nao`: foi fornecida uma autorização anterior; nunca mostra seu conteúdo |

Os valores em milissegundos são arredondados independentemente; a soma das
etapas pode diferir dos totais exibidos.

`restore solicitado sim` não comprova que o portal reutilizou a autorização
nem que dispensou o diálogo. Essas medições não incluem consultas iniciais de
capacidades, leitura da autorização, fechamento da sessão ou apresentação da
sobreposição; não são a latência completa do atalho até o quadro visível.
Não compare uma captura com diálogo a outra sem diálogo como se fossem o
mesmo cenário. O diagnóstico não altera consentimento, backend, timeout ou
fallback e não expõe tokens ou conteúdo da tela.

**Consulta de capacidades.** A captura tenta ler `version`,
`AvailableSourceTypes` e `AvailableCursorModes` em uma única chamada
`Properties.GetAll`. Valores não ficam em cache entre capturas. Se `GetAll`
falhar, não for suportado ou devolver propriedades ausentes/tipos inesperados,
a conexão usa as leituras `Get` anteriores, preservando ordem e saídas
antecipadas; uma conexão nova volta a tentar `GetAll`. A primeira sondagem
incompatível custa uma chamada extra, mas não é repetida nessa conexão.
Permissões, consentimento, cursor, persistência e fallback de captura não mudam.

A linha `ScreenCast capacidades: ... ms; consulta GetAll` indica leitura
agrupada; `consulta Get` indica compatibilidade pelas leituras individuais.
Esse tempo separado não faz parte dos totais legados e não é a latência
completa do atalho. Menos consultas não comprova, por si só, melhora perceptível
ou a meta de desempenho no seu desktop.

### Linux com X11

Atalhos e captura funcionam sem permissões adicionais (XGrabKey e XGetImage).
O comando `pixelpointer grant` não é necessário no X11. Se outra aplicação já
registrou Ctrl+1 ou Ctrl+2, o daemon informa o conflito e não anuncia o atalho
como disponível. Em modo de desenho, um compositor X11 detectado permite
o caminho de anotações transparentes sobre o conteúdo atual, ainda sujeito ao
aceite gráfico descrito no roteiro abaixo. Sem compositor, o aplicativo
avisa e usa uma captura congelada; essa alternativa não é transparência ao vivo.

### Execução em segundo plano e encerramento

Esc ou o fechamento da sobreposição encerra somente a sessão visual; o daemon
continua ativo. Use `pixelpointer status` para consultar o estado e
`pixelpointer quit` para encerrar o daemon, inclusive quando uma sobreposição
estiver aberta. O menu da tray oferece **Zoom**, **Desenhar** e **Sair** quando
o SDL e o desktop disponibilizam a tray. Ctrl+C ou SIGTERM também encerra o
daemon. Se a tray estiver indisponível (por exemplo, em desktops sem suporte a
ícones de bandeja), use `status` e `quit`; no GNOME, uma extensão de tray pode
ser necessária.
A mensagem **Tray registrada** confirma o registro no backend, não a exibição
efetiva no painel. O daemon deve permanecer em execução após essa mensagem.

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

Se o daemon estiver em execução, encerre-o primeiro com `pixelpointer quit`.
Depois, remova o PixelPointer pelo mesmo método usado para instalá-lo:

| Instalação | Remoção |
| --- | --- |
| `install.sh` (Linux/macOS, prefixo padrão) | No Linux, execute `pixelpointer uninstall-desktop-file` se registrou a entrada do menu; depois `rm -f -- "$HOME/.local/bin/pixelpointer"` e `rm -r -- "$HOME/.local/lib/pixelpointer"` |
| `install.sh --prefix <dir>` | Remova `<dir>/bin/pixelpointer` e `<dir>/lib/pixelpointer`, substituindo `<dir>` pelo prefixo usado na instalação |
| `install.sh --global` | Remova `/usr/local/bin/pixelpointer` e `/usr/local/lib/pixelpointer` com `sudo`; antes, remova a entrada desktop no Linux pelo binário instalado |
| `install.ps1` | PowerShell: `Remove-Item -LiteralPath "$env:LOCALAPPDATA\Programs\pixelpointer" -Recurse` |
| `.deb` | `sudo apt remove pixelpointer` |
| Homebrew | `brew uninstall pixelpointer` |
| winget | `winget uninstall lzocateli.PixelPointer` |

Se adicionou manualmente a linha de inicialização ao perfil do PowerShell, remova
essa linha de `$PROFILE.CurrentUserAllHosts`. No Linux, se tiver usado
`install-desktop-file`, execute `pixelpointer uninstall-desktop-file` **antes de
remover o executável**, com o mesmo binário e `XDG_DATA_HOME`. O comando remove
somente a entrada e o ícone idênticos aos recursos esperados pela instalação
atual. Se encontrar arquivo modificado, link simbólico ou recurso de outra
versão/localização, recusa a operação e preserva os arquivos para revisão manual.
Não remove diretórios nem capturas PNG. O pacote `.deb` gerencia separadamente os
recursos instalados em `/usr/share`.
No GNOME, os atalhos vinculados podem ser
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
| Registro do app id falhou | Verifique a entrada desktop com `pixelpointer install-desktop-file` e consulte `pixelpointer probe`; ausência da interface Registry é informada como limitação do portal, não como erro de instalação |
| Atalhos indisponíveis no Wayland | Desktop sem o portal GlobalShortcuts: use um atalho do desktop (tabela acima) |
| A janela abre mas não recebe o teclado | Compositor sem activation token; clique na janela uma vez |
| Daemon aborta após registrar a tray, com `_dbus_check_is_valid_utf8` | Corrigido o tempo de vida dos textos do menu no SDL 3.5.0; use um pacote reconstruído após essa correção. A interrupção do daemon também inutiliza os atalhos registrados |
| Tray registrada, mas ícone ausente no GNOME | Confirme primeiro que `status` responde e o daemon não abortou. A exibição depende do suporte StatusNotifier/AppIndicator do painel; sem ele, use a CLI. O aplicativo não instala extensões |
| Ctrl+S informa destino inválido ou inacessível | Verifique a configuração da pasta de imagens/capturas e suas permissões; o aplicativo não tenta outro destino |
| `selftest falhou` no Linux | glibc anterior a 2.34 ou `libfontconfig` ausente: use Ubuntu 22.04, Debian 12, RHEL 9 ou mais novos e instale `libfontconfig1` |
| Atraso de ~0,5 s ao abrir no Wayland | Custo do portal Screenshot; a captura contínua por PipeWire está planejada para a F2 |

Para relatar um problema, inclua a saída de `pixelpointer probe` e siga o
[suporte](../.github/SUPPORT.md).

### Recuperar depois de um abort do daemon

Um abort nativo pode deixar o socket no disco, embora o processo e os grabs X11
tenham terminado. O próximo daemon recusa esse caminho por segurança; não o
remove automaticamente. Execute `pixelpointer status` e verifique os processos
do seu usuário com o monitor do sistema. Somente depois de confirmar que não há
daemon ativo e que o caminho pertence ao seu usuário, remova **apenas**
`$XDG_RUNTIME_DIR/pixelpointer.sock` (quando `XDG_RUNTIME_DIR` estiver definido)
ou `~/.cache/pixelpointer/run/pixelpointer.sock` (fallback). Não remova diretórios
nem sockets de outras aplicações. Reinicie o daemon pelo pacote corrigido.

## Limitações atuais

- Timer (Ctrl+3), Live Zoom (Ctrl+4), gravação (Ctrl+5), recorte (Ctrl+6) e
  DemoType (Ctrl+7) ainda não existem; veja o [plano](plano-implementacao.md).
- Em várias telas no Wayland, o compositor escolhe o monitor da janela.
- macOS sem atalhos globais nativos e sem assinatura na versão 1.

## Roteiro manual de aceite no X11

Use uma sessão **X11 real**, sem `SDL_VIDEODRIVER=dummy`, com conteúdo de teste
sem dados pessoais. O roteiro requer o binário Native AOT linux-x64 da compilação
local (para outra arquitetura, gere o RID correspondente). A partir da raiz do
repositório, gere e valide em containers:

```bash
pwsh ./tools/scripts/Invoke-Validation.ps1 -Aot -Engine podman
pwsh ./tools/scripts/Publish-Aot.ps1 -Rid linux-x64 -Engine podman
pwsh ./tools/scripts/Test-AotPackage.ps1 -Rid linux-x64 -Engine podman
```

Resultado esperado: build e testes aprovados e pacote válido. Os comandos geram
artefatos locais; não instalam o aplicativo no host nem comprovam o fluxo gráfico.
Docker pode substituir Podman com `-Engine docker`.

Na sessão gráfica do usuário, a partir da raiz do repositório, use o pacote
gerado, sem `sudo`:

```bash
./artifacts/publish/linux-x64/pixelpointer probe
./artifacts/publish/linux-x64/pixelpointer grant
./artifacts/publish/linux-x64/pixelpointer daemon
```

1. **Diagnóstico:** `probe` deve identificar X11 e informar se há compositor.
   `grant` deve explicar que a captura nativa X11 dispensa permissão do portal.
   Falta de Registry não deve recomendar reinstalar a entrada desktop; erro real
   de registro deve continuar distinguível.
2. **Atalhos e captura:** pressione Ctrl+1, anote sobre uma imagem conhecida,
   amplie e feche com Esc. O daemon deve continuar ativo. Compare com
   `./artifacts/publish/linux-x64/pixelpointer zoom` em outro terminal.
   Repita Ctrl+1 e Ctrl+2 nas quatro combinações de CapsLock/NumLock.
3. **Desenho ao vivo:** com compositor, deixe um relógio ou animação visível e
   pressione Ctrl+2. O conteúdo deve continuar se movendo sob os traços, sem
   fundo opaco. W/K devem alternar somente os quadros explicitamente acionados.
   Sem compositor, espere aviso e captura congelada, não transparência.
4. **PNG:** pressione Ctrl+S duas vezes e confira a mensagem, os dois arquivos
   distintos e a pasta XDG configurada. No zoom, o PNG deve conter a captura e os
   traços; no desenho transparente, deve preservar o alfa. Com destino sem
   permissão, espere erro na sobreposição e nenhum arquivo em outra pasta.
   Use um usuário sem privilégios para esse teste.
5. **Tray e saída:** abra e feche Zoom/Desenhar pela tray. Use **Sair** com e sem
   sobreposição aberta. Se não houver suporte à tray, confirme o aviso e use
   `./artifacts/publish/linux-x64/pixelpointer quit` em outro terminal.
   Consulte `status` antes e depois; após sair, o retorno deve ser 1 e nenhum
   daemon deve permanecer.
6. **Instância única e reinício:** com o daemon ativo, execute `daemon` em outro
   terminal. Ele deve falhar sem afetar a primeira instância. Encerre a primeira
   e reinicie; os atalhos devem voltar sem grabs residuais.
7. **Menu sem terminal:** encerre o daemon, execute `install-desktop-file` pelo
   mesmo binário e abra **PixelPointer** no menu de aplicativos. Confirme ícone,
   Ctrl+S com feedback visível e saída pela tray/CLI, sem terminal aberto.
   Se houver conflito com uma entrada existente, preserve-a e revise a mensagem;
   não apague arquivos para forçar a instalação. Para desfazer apenas essa
   integração, execute `uninstall-desktop-file` pelo mesmo binário.
8. **Conflitos e monitores:** em uma sessão de teste, reserve Ctrl+1 para outro
   aplicativo antes de iniciar o daemon; confirme o conflito e que Ctrl+2 ainda
   funciona, se livre. Repita captura, alinhamento dos traços e salvamento com
   monitores de escalas/resoluções diferentes e posições negativas.

Registre SO, arquitetura, desktop/compositor, versão do PixelPointer, escala,
monitores e resultado de cada etapa; não publique capturas sensíveis, tokens ou
logs integrais. Compositor, tray, atalhos físicos e múltiplos monitores ainda
dependem desse aceite manual; SDL dummy não os valida.
