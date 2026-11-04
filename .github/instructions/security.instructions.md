---
name: "Segurança do PixelPointer"
description: "Use ao alterar captura de tela, atalhos globais, IPC, portais XDG, permissões do SO, execução de processos, instaladores, dependências, distribuição ou logs do PixelPointer."
applyTo: "src/**, tools/**, install/**, .github/workflows/**"
---

# Segurança

- Capturas de tela podem conter dados sensíveis: ficam somente em memória; nada é enviado pela rede nem gravado em disco sem ação explícita do usuário (Ctrl+S, gravação).
- Arquivos temporários de captura criados por portais ou ferramentas do SO são removidos logo após a leitura.
- O socket IPC fica em `$XDG_RUNTIME_DIR` (ou diretório temporário do usuário), aceita apenas comandos conhecidos e nunca executa entrada recebida como código ou caminho.
- Permissões do SO (portal Screenshot/GlobalShortcuts, Gravação de Tela e Acessibilidade no macOS) são pedidas com explicação, nunca contornadas gravando diretamente em PermissionStore ou TCC.
- Injeção de teclas (DemoType) só envia texto de scripts escolhidos pelo usuário.
- Instaladores conferem SHA-256 com o `SHA256SUMS` da release, não se autoelevam, não alteram PATH nem perfis e recusam substituir arquivos que não criaram.
- Releases publicam `SHA256SUMS`, SBOM e atestações Sigstore keyless de proveniência e SBOM; não há Authenticode nem Developer ID na v1 (D5).
- O atualizador (`pixelpointer update`, [ADR 2026-0001](../../docs/adr/2026-0001-atualizacao-opt-in.md)) é opt-in, só substitui instalações de usuário com `install-receipt.json`, gravação do usuário e processo não elevado, e só executa o pacote depois de SHA-256 e proveniência Sigstore verificados nativamente contra a identidade de `gw-github-assets.yml`. Mudar essa identidade, o recibo ou o material de `SigstoreTrustRoot` exige atualizar a política, os testes e a documentação juntos; nunca relaxe a verificação para aceitar pacotes sem atestação.
- Logs não incluem conteúdo capturado, tokens de portal ou caminhos privados além do necessário.
- Dependências novas exigem licença compatível registrada em `Directory.Packages.props` e em `THIRD-PARTY-NOTICES.md` quando redistribuídas.
