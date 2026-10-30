# Contribuindo com o PixelPointer

## Antes de começar

1. Leia o [README](../README.md) e os documentos da área afetada.
2. Procure a Issue e os ADRs relacionados.
3. Confirme o escopo e a autorização da mudança antes de executar.
4. Nunca inclua secrets em nenhuma parte do código ou configuração, inclusive
   workflows, testes, documentação, exemplos, scripts e connection strings.
   Secrets locais permanecem exclusivamente no arquivo externo
   `$env:APPDATA\Microsoft\UserSecrets\PixelPointer\.env`, sem leitura por automações, e secrets de CI
   permanecem nos Environments do GitHub.
5. Instale o hook obrigatório de detecção de secrets em cada clone:

   ```powershell
   ./tools/scripts/install-gitleaks-hook.ps1
   git hook run pre-commit
   ```

   Consulte a [política de detecção de secrets](SECRET-SCANNING.md).
   O hook usa `.gitleaks.toml`; não o substitua por uma execução sem a
   configuração versionada.

## Licenciamento de contribuições

O código é distribuído sob `MIT`; consulte o arquivo [LICENSE](../LICENSE).

## Fluxo

Os formulários usam as labels declaradas em [labels.json](labels.json).
Gerar arquivos não cria labels nem habilita controles no GitHub. O mantenedor
deve executar `groundwork labels` com GitHub CLI autenticado para diagnosticar
as ausentes e, se aprovado, `groundwork labels --sync --dry-run` seguido de
`groundwork labels --sync`. Labels existentes não são renomeadas, removidas
nem têm cores e descrições sobrescritas.

1. Abra ou referencie uma Issue que descreva o problema e o resultado esperado.
2. Atualize testes, contratos, documentação e telemetria necessários; valide o menor recorte primeiro e registre evidências no PR.
3. Use os [Conventional Commits](COMMIT_CONVENTIONS.md). Push, merge, publicação e deploy exigem autorização separada.

## Definition of Done

- escopo e autorização rastreáveis, com evidências no artefato de origem;
- critérios de aceite demonstrados;
- código formatado, analisado e compilado;
- testes reais proporcionais ao risco;
- contratos e clientes gerados sincronizados;
- segurança, acessibilidade, concorrência e desempenho considerados;
- logs e métricas não expõem conteúdo sensível;
- hook Gitleaks instalado e `Gitleaks / Full History` aprovado;
- rollout, rollback e compatibilidade documentados;
- nenhum achado crítico ou alto aberto sem aceitação formal.

## Pull requests

Mantenha PRs coesos e pequenos o bastante para revisão responsável. Mudanças incompatíveis exigem issue, ADR quando aplicável, estratégia de migração e período de compatibilidade. Não misture refactor amplo com mudança funcional.
