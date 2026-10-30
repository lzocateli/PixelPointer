# Suporte

O PixelPointer é mantido pela comunidade, sem SLA ou suporte comercial declarado.

Antes de abrir uma solicitação, consulte o [README](../README.md), o
[guia de uso](../docs/uso.md), as
[releases](https://github.com/lzocateli/PixelPointer/releases) e as
[Issues existentes](https://github.com/lzocateli/PixelPointer/issues).

Use o [seletor de Issues](https://github.com/lzocateli/PixelPointer/issues/new/choose):

| Necessidade | Canal |
| --- | --- |
| Comportamento incorreto reproduzível | Relatar defeito |
| Nova capacidade ou melhoria de uso, desempenho ou documentação | Propor funcionalidade ou melhoria |
| Pergunta de instalação, uso ou contribuição | Dúvida ou suporte |
| Manutenção delimitada | Tarefa técnica |
| Investigação de alternativas | Spike ou decisão arquitetural |
| Vulnerabilidade | [Relato privado](SECURITY.md), nunca Issue pública |

Informe a versão do PixelPointer (`pixelpointer --version`), SO/distribuição,
arquitetura, sessão (X11/Wayland) e desktop, a saída de `pixelpointer probe`,
o comando executado e o resultado esperado. Compartilhe somente saída mínima
sanitizada; nunca anexe capturas de tela com dados pessoais ou de terceiros,
secrets ou dumps. Use caminhos fictícios nos exemplos.

Problemas de permissão no GNOME/Wayland costumam ser resolvidos com
`pixelpointer install-desktop-file` seguido de `pixelpointer grant`; consulte o
[guia de uso](../docs/uso.md#solução-de-problemas).

Issues recebem triagem do mantenedor; não há garantia de atendimento ou aceite.
Contribuições seguem [CONTRIBUTING](CONTRIBUTING.md) e a
[governança](GOVERNANCE.md).
