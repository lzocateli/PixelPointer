---
name: "Integridade da fonte de release"
description: "Use ao alterar CI, RC, snapshots, tags, publicação, distribuição, republish, aceite de release ou seus testes para impedir pacotes desalinhados da fonte."
applyTo: ".github/workflows/**,tools/scripts/**,tests/**"
---

# Integridade da fonte de release

- Antes de alterar o fluxo, leia `docs/release-integrity.md` e o contrato `.github/CI-CD.md`.
- Toda distribuição exige autorização humana na mesma execução/SHA/tag antes do envio público. O padrão é revisão nativa de `distribution`; a alternativa `manual-dispatch` exige opt-in versionado, repositório privado, estável, dispatch de administrador humano e confirmação exata de tag/SHA. Nunca faça fallback automático por erro de plano. Preserve prova nos publicadores/finalização; quality, merge, bot ou waiver não autorizam envio. Snapshots só enviam a pacotes internos.
- Alterações de workflows `gw-*` e `tools/scripts/ci/` são feitas no catálogo Groundwork; aplique a atualização por reconciliação, nunca edite hashes para esconder desvios.
- Novas tags e publicações exigem SHA igual ao HEAD remoto atual e `Result` verde da execução CI em `main` no mesmo SHA, aplicativo GitHub Actions e check suite. Não aceite apenas ancestralidade ou CI de PR.
- Revalide depois de aprovações e antes de criar tags/publicar. Fixe SHA, versão e cabeça do Release PR; mudança exige nova resolução e novo aceite.
- PR comum, inclusive fix, integra pelo CI normal. Não bloqueie porque existem outros PRs abertos e não reutilize o aceite de RC anterior após mudar código.
- Dependências obrigatórias ficam no array versionado `releaseDependencies` de `.github/ci-cd.json`; `Release-Depends-On: #123, #456` no corpo do Release PR pode complementá-lo. Exija merge na branch/repositório corretos e inclusão do commit resultante, inclusive squash. Fechamento sem merge não satisfaz; nunca apague a dependência para liberar um gate.
- Releases finais exigem candidata aceita na base exata e diff limitado aos metadados configurados. Não use igualdade literal de SHA RC/final: o merge/bump muda o commit.
- `Preview-Waiver` dispensa somente aceite de RC; nunca SHA atual, CI verde ou dependências obrigatórias.
- Snapshots rodam depois de `Result`, não entram em seus `needs` e só aceitam CI ainda em andamento se for a própria execução, já com `Result` verde. Evite ciclos de dependência.
- Republish histórico só pode recuperar artefatos ausentes de uma release estável já pública e checkout da tag imutável. Rascunho não é exceção. Redeploy/rollback preservam tags históricas e não exigem HEAD atual.
- Sementes `ci.yml`/`publish.yml` não são atualizadas automaticamente: revise permissões e propagação de `republish` no consumidor.
- Consultas incompletas, CI pendente/falho ou fonte diferente devem falhar fechado com SHA/PR e recuperação explícitos; não transforme a recusa em warning/success ou remova a guarda.
- Cubra avanço da main durante aprovação/validação, CI errado/ausente, squash, diferença RC/final, waiver e republish histórico. Valide scripts e workflows renderizados em containers.