---
name: "Testes do PixelPointer"
description: "Use ao criar ou alterar testes unitários, de integração, E2E gráficos isolados com uv e vídeo, de pacote ou de instaladores do PixelPointer e matrizes por SO."
applyTo: "tests/**, **/*Tests.cs"
---

# Testes

- xUnit v3 sobre Microsoft Testing Platform (`dotnet test --solution PixelPointer.slnx`); sem adapters VSTest.
- Unitários cobrem lógica pura: protocolo IPC, caminhos de Request dos portais, resolução de bibliotecas nativas, modelo de anotações e transformações de zoom.
- Renderização é testada com Skia em CPU contra imagens de referência determinísticas, sem janela nem GPU.
- Tela, portais e atalhos reais continuam fora dos testes unitários e do smoke com SDL `dummy`.
- E2E gráficos isolados estão autorizados pelo solicitante em 2026-10-30, nesta conversa, para simular interação nativa e gravar evidências. Essa autorização altera a restrição anterior, mas não representa implementação nem aceite de uma suíte.
- Execute E2E gráficos em suíte separada, com aplicação, runner e desktop descartáveis em containers, sem acessar a sessão gráfica, D-Bus, diretórios pessoais ou dispositivos de entrada do host. Não use containers privilegiados nem contorne consentimentos do sistema.
- Quando a aplicação estiver em execução para validação, mantenha nginx em container como borda de qualquer acesso HTTP do navegador ou runner; não exponha diretamente serviços auxiliares.
- Vídeos são evidências complementares: exija asserções objetivas, prazos, resultado explícito, preservação das evidências de falha e limpeza apenas dos recursos da execução. Fixe ambiente e dependências com licenças verificadas; não grave secrets nem conteúdo pessoal.
- Siga o [plano exclusivo de E2E gráficos](../../docs/plano-e2e-grafico-uv.md). O [roteiro manual da F0](../../docs/f0-relatorio.md#6-roteiro-de-teste-manual-por-plataforma) continua necessário para hardware e combinações ainda não cobertas; não declare paridade entre desktops a partir de um ambiente virtual.
- `selftest` é o contrato de smoke dos pacotes: Skia desenha e codifica um PNG e o SDL inicializa com o driver `dummy`.
- Pacotes são testados em containers limpos sem .NET (Ubuntu 22.04, Rocky 9, Debian 12) e em runners Windows/macOS, também pelo link simbólico do comando; o `.deb` é instalado e removido com `apt-get`.
- Instaladores são testados contra servidor HTTP local, como root e como usuário sem privilégio, incluindo checksum adulterado e recusa de arquivos de outros aplicativos.
- Declare a matriz SO × arquitetura × sessão (X11/Wayland, desktop) com resultado; marque combinações não verificadas.
