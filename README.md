# PixelPointer — zoom e anotação de tela para apresentações

**Português (Brasil)** · [English](README.en.md) · [Español](README.es.md)

[![CI](https://github.com/lzocateli/PixelPointer/actions/workflows/ci.yml/badge.svg)](https://github.com/lzocateli/PixelPointer/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/lzocateli/PixelPointer?sort=semver)](https://github.com/lzocateli/PixelPointer/releases/latest)
[![Licença MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-blue)](LICENSE)
![Plataformas: Linux, Windows e macOS](https://img.shields.io/badge/plataformas-Linux%20%7C%20Windows%20%7C%20macOS-informational)

Ferramenta Native AOT (`pixelpointer`) para quem apresenta, grava ou ensina:
amplia a tela com o mouse, desenha e destaca por cima e volta ao trabalho com
um toque, acionada por **atalhos globais** no Linux (X11 e **Wayland**), no
Windows e no macOS. Um único pacote, sem .NET instalado.

**Para quem é:** palestrantes, professores, criadores de vídeos e equipes que
fazem demonstrações ao vivo e querem o fluxo do ZoomIt (Sysinternals/PowerToys)
em qualquer sistema operacional, inclusive em desktops Wayland como GNOME e KDE.
O PixelPointer é um projeto independente, não é um produto da Microsoft e não
usa o código, a marca ou os ícones do ZoomIt.

> **Estado:** pré-1.0. Zoom (Ctrl+1) e desenho (Ctrl+2) funcionam e foram
> validados no GNOME 50/Wayland; Windows e macOS passam no CI e aguardam o
> roteiro de teste manual. Timer, Live Zoom, recorte, gravação e DemoType estão
> no [plano de implementação](docs/plano-implementacao.md). Resultados e
> limitações: [relatório da F0](docs/f0-relatorio.md).

## Instalação

Pacotes para `linux-x64`, `linux-arm64` (glibc 2.34+: Ubuntu 22.04, Debian 12,
RHEL/Rocky 9 ou mais novos), `win-x64`, `win-arm64`, `osx-arm64` e `osx-x64`.
Os instaladores conferem o SHA-256 com o `SHA256SUMS` da release, não se
autoelevam e não alteram perfis nem o `PATH`:

```bash
# Linux e macOS: aplicativo em ~/.local/lib/pixelpointer, comando em ~/.local/bin
curl -fsSL https://github.com/lzocateli/PixelPointer/releases/latest/download/install.sh | sh
```

```powershell
# Windows (PowerShell 5.1+): instala em %LOCALAPPDATA%\Programs\pixelpointer
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
& ([scriptblock]::Create((Invoke-RestMethod https://github.com/lzocateli/PixelPointer/releases/latest/download/install.ps1)))
```

```bash
# Homebrew (macOS e Linux)
brew install lzocateli/tap/pixelpointer

# winget (Windows), após a moderação da versão no microsoft/winget-pkgs
winget install lzocateli.PixelPointer

# Debian/Ubuntu (amd64 ou arm64), a partir do asset da release
curl -fsSLO https://github.com/lzocateli/PixelPointer/releases/latest/download/pixelpointer-linux-x64.deb
sudo apt install ./pixelpointer-linux-x64.deb
```

Cada pacote traz o SBOM SPDX e atestações de proveniência:
`gh attestation verify <arquivo> --repo lzocateli/PixelPointer`
([verificação](docs/release.md#verificação-pelo-usuário)).

## Desinstalar

Se estiver em execução, encerre primeiro o daemon com `pixelpointer quit`. Depois,
remova o PixelPointer pelo mesmo método usado para instalá-lo:

| Instalação | Comando ou ação |
| --- | --- |
| `install.sh` (Linux/macOS, prefixo padrão) | No Linux, `pixelpointer uninstall-desktop-file` se registrou a entrada do menu; depois `rm -f -- "$HOME/.local/bin/pixelpointer"` e `rm -r -- "$HOME/.local/lib/pixelpointer"` |
| `install.sh --prefix <dir>` | Remova `<dir>/bin/pixelpointer` e `<dir>/lib/pixelpointer`; substitua `<dir>` pelo prefixo usado na instalação |
| `install.sh --global` | Remova `/usr/local/bin/pixelpointer` e `/usr/local/lib/pixelpointer` com `sudo`; antes, remova a entrada desktop no Linux pelo próprio binário |
| `install.ps1` | PowerShell: `Remove-Item -LiteralPath "$env:LOCALAPPDATA\Programs\pixelpointer" -Recurse` |
| `.deb` | `sudo apt remove pixelpointer` |
| Homebrew | `brew uninstall lzocateli/tap/pixelpointer` |
| winget | `winget uninstall lzocateli.PixelPointer` |

Se adicionou manualmente a linha de inicialização ao perfil do PowerShell, remova
essa linha do `$PROFILE.CurrentUserAllHosts`. A integração desktop Linux deve ser
removida antes do executável com `pixelpointer uninstall-desktop-file`, usando o
mesmo `XDG_DATA_HOME`; o comando preserva arquivos modificados. A desinstalação
não apaga capturas PNG. Detalhes e limites por método estão no
[guia de uso](docs/uso.md#desinstalar).

## Primeiros passos

```bash
pixelpointer install-desktop-file   # Linux: registra o app id usado pelos portais
pixelpointer grant                  # GNOME/Wayland: permissão de captura (uma vez)
pixelpointer daemon                 # Ctrl+1 zoom, Ctrl+2 desenho
```

O comando `daemon` permanece em **primeiro plano**: mantém os atalhos e o IPC
ativos, mas não libera o terminal automaticamente. Isso é esperado, não um
travamento; Ctrl+C encerra o processo. No Linux, para liberar o prompt:

```bash
pixelpointer daemon &
```

O `&` coloca o processo em segundo plano, mas ele ainda pode escrever no terminal
e encerrar quando a janela do terminal for fechada. Para usar sem terminal no
Linux, execute `pixelpointer install-desktop-file` e abra **PixelPointer** pelo
menu de aplicativos. Encerre por **Sair** na tray ou por `pixelpointer quit`.
Essa integração não configura início automático com a sessão.

| Atalho ou tecla | Ação |
| --- | --- |
| **Ctrl+1** | Zoom: congela a tela ampliada; o mouse percorre a imagem |
| **Ctrl+2** | Desenho sem zoom |
| `pixelpointer status` | Consulta se o daemon está em execução |
| `pixelpointer quit` | Encerra o daemon e a sobreposição ativa |
| Roda do mouse / setas | Mais ou menos zoom |
| Clique e arraste | Desenhar (caneta com pressão é suportada) |
| R G B Y O P | Vermelho, verde, azul, amarelo, laranja, rosa |
| Ctrl+roda | Espessura da caneta |
| Ctrl+Z / E | Desfazer / apagar tudo |
| W / K | Quadro branco / quadro preto |
| Ctrl+S | Salvar PNG |
| Esc ou botão direito | Sair |

No Wayland, o primeiro `daemon` pede a confirmação dos atalhos ao desktop.
Sem o portal de atalhos, use um atalho do próprio desktop chamando
`pixelpointer zoom` ou `pixelpointer draw`. Detalhes, permissões por sistema e
solução de problemas: [guia de uso](docs/uso.md). Esc fecha somente a
sobreposição; o menu da tray oferece **Sair** quando disponível, e
`pixelpointer quit` encerra o daemon. O aplicativo não inicia automaticamente
com a sessão. O desenho ao vivo requer compositor X11; no Wayland usa captura
congelada.

Ctrl+S respeita a pasta de capturas do usuário: Imagens/`Screenshots` no Windows,
preferência de captura ou Área de Trabalho no macOS e `PICTURES/Screenshots`
conforme XDG no Linux. Não sobrescreve arquivos e informa o destino na
sobreposição. Detalhes e exportação com alfa:
[onde o Ctrl+S salva](docs/uso.md#onde-o-ctrls-salva).

## Como funciona

| Camada | Tecnologia |
| --- | --- |
| Janela, entrada e caneta | SDL3 (Wayland nativo, X11, Win32, Cocoa) |
| Desenho e zoom | SkiaSharp |
| Captura e atalhos no Wayland | Portais XDG (Screenshot, GlobalShortcuts) via D-Bus |
| Captura e atalhos no X11, Windows e macOS | XGetImage/XGrabKey, GDI/RegisterHotKey, screencapture |
| Distribuição | .NET 10 Native AOT, ~19 MB com as bibliotecas nativas |

## Desenvolvimento

- .NET 10 (SDK do `global.json`) com Native AOT; testes com xUnit v3 sobre
  Microsoft Testing Platform: `dotnet test --solution PixelPointer.slnx`.
- Valide como no CI (PowerShell 7 e Docker ou Podman; `-Aot` publica e executa
  o `selftest` do binário nativo `linux-x64`):

  ```bash
  pwsh ./tools/scripts/Invoke-Validation.ps1 -Aot
  ```

- Pacotes de release, smoke em distribuições limpas e testes dos instaladores:
  [processo de release](docs/release.md).
- No Windows e no macOS: `pwsh ./tools/scripts/Invoke-Validation.ps1 -Native -Aot`.
- Instale o hook de detecção de secrets:
  `pwsh ./tools/scripts/install-gitleaks-hook.ps1`.
- Leia [CONTRIBUTING](.github/CONTRIBUTING.md), a
  [convenção de commits](.github/COMMIT_CONVENTIONS.md) e a
  [política de segurança](.github/SECURITY.md).

## Comunidade e contribuições

Contribuições por fork e pull request são bem-vindas. Comece por
[bugs, melhorias ou dúvidas](https://github.com/lzocateli/PixelPointer/issues/new/choose)
e aguarde o alinhamento de escopo antes de implementar.

- [Guia de contribuição](.github/CONTRIBUTING.md) e [governança](.github/GOVERNANCE.md).
- [Suporte](.github/SUPPORT.md) e [código de conduta](.github/CODE_OF_CONDUCT.md).
- [Relatos privados de vulnerabilidade](.github/SECURITY.md).
- [Histórico de versões](https://github.com/lzocateli/PixelPointer/releases).

A infraestrutura gerenciada (workflows `gw-*`, `.githooks/`, `tools/scripts/ci/`,
templates e políticas em `.github/`) foi gerada pelo Altivy.Groundwork;
`.groundwork/manifest.json` lista esses arquivos e `groundwork status` confere sua
integridade. Os workflows e scripts de build, publicação e distribuição
(`ci.yml`, `publish.yml`, `post-release.yml`, `winget.yml`, `tools/scripts/*.ps1`,
`install/`) e os documentos de governança foram adaptados do DotnetScope e
pertencem ao projeto.

## Licença

[MIT](LICENSE). Componentes redistribuídos (SDL3, SkiaSharp/Skia, Tmds.DBus):
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
