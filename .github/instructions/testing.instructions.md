---
name: "Testes do PixelPointer"
description: "Use ao criar ou alterar testes unitários, de integração, de pacote ou de instaladores do PixelPointer e matrizes por SO."
applyTo: "tests/**, **/*Tests.cs"
---

# Testes

- xUnit v3 sobre Microsoft Testing Platform (`dotnet test --solution PixelPointer.slnx`); sem adapters VSTest.
- Unitários cobrem lógica pura: protocolo IPC, caminhos de Request dos portais, resolução de bibliotecas nativas, modelo de anotações e transformações de zoom.
- Renderização é testada com Skia em CPU contra imagens de referência determinísticas, sem janela nem GPU.
- Nada de tela, portal ou atalho real em teste automatizado; isso fica no roteiro manual do [relatório da F0](../../docs/f0-relatorio.md#6-roteiro-de-teste-manual-por-plataforma).
- `selftest` é o contrato de smoke dos pacotes: Skia desenha e codifica um PNG e o SDL inicializa com o driver `dummy`.
- Pacotes são testados em containers limpos sem .NET (Ubuntu 22.04, Rocky 9, Debian 12) e em runners Windows/macOS, também pelo link simbólico do comando; o `.deb` é instalado e removido com `apt-get`.
- Instaladores são testados contra servidor HTTP local, como root e como usuário sem privilégio, incluindo checksum adulterado e recusa de arquivos de outros aplicativos.
- Declare a matriz SO × arquitetura × sessão (X11/Wayland, desktop) com resultado; marque combinações não verificadas.
