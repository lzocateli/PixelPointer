# PixelPointer - Instruções globais

Toda documentação produzida ou atualizada deve estar em português brasileiro (pt-BR). Preserve
comandos, identificadores, caminhos e marcadores literais exigidos pelas ferramentas.

## Contexto obrigatório

- O PixelPointer é uma ferramenta Native AOT de zoom, anotação e gravação de tela para apresentações, com atalhos globais no Linux (X11 e Wayland), Windows e macOS.
- Runners de testes e ferramentas de build executam em containers; comandos Docker no host apenas os orquestram.
- Sempre que um teste, preview ou validação exigir a aplicação em execução, suba seus serviços em containers e acesse HTTP do navegador e do runner exclusivamente pela borda de um proxy reverso nginx também em container. Testes unitários sem aplicação HTTP em execução continuam em containers e não exigem nginx ocioso.
- Leia o [README](../README.md) antes de alterar contratos, identidade, segurança ou deployment.

## Fluxo de trabalho

1. Comece pelo comportamento, teste relacionado e módulo proprietário.
2. Declare uma hipótese local e uma validação capaz de refutá-la.
3. Faça a menor alteração coerente e valide o recorte imediatamente.
4. Valide primeiro o recorte alterado; depois execute verificações amplas proporcionais ao risco.
5. Atualize contrato, compatibilidade, migração, documentação, segurança e observabilidade quando o comportamento exigir.
6. Preserve alterações existentes e não reformate arquivos fora do escopo.

- ADRs ficam em `docs/adr/AAAA-NNNN-titulo.md`, começam como `proposed` e seguem `.github/ADR_TEMPLATE.md`.
- IA não marca ADR como `accepted` sem decisão ou evidência humana explícita e referenciada.

## Commits

- Todo commit criado ou sugerido segue [Conventional Commits](COMMIT_CONVENTIONS.md) e usa os tipos e escopos aprovados pelo projeto.
- Escreva a descrição em português brasileiro, no imperativo, iniciando com minúscula e sem ponto final.
- Mudanças incompatíveis usam `!` no cabeçalho e o rodapé `BREAKING CHANGE:` com impacto e migração.
- Não faça commit sem solicitação explícita. Push, publicação e deploy sempre exigem solicitação explícita.

## Regras invariáveis

- Preserve conteúdo do usuário; conflitos nunca podem causar sobrescrita silenciosa.
- Normalize e autorize paths antes de qualquer acesso ao filesystem.
- No desenvolvimento local em Windows, armazene todo secret exclusivamente em `$env:APPDATA\Microsoft\UserSecrets\PixelPointer\.env`; em Linux/macOS, use `~/.microsoft/usersecrets/PixelPointer/.env`.
- Nunca crie arquivos `.env`, `.env.*`, `secrets.json`, cópias ou templates com valores reais dentro do repositório. Não use `dotnet user-secrets`; scripts, Compose, coleções HTTP, testes e documentação devem referenciar o arquivo externo ou variáveis de ambiente injetadas por processo seguro.
- Não leia, exiba, registre, copie ou sobrescreva o conteúdo do arquivo externo durante validações. Antes de mover ou gravar secrets, falhe fechado se o destino já existir.
- Não registre tokens, certificados privados, variáveis de ambiente, secrets ou logs integrais.
- O hook `.githooks/pre-commit` e o workflow `gw-gitleaks.yml` são obrigatórios; nunca use `--no-verify`. Consulte [SECRET-SCANNING.md](SECRET-SCANNING.md).
- Workflows e scripts `gw-*` e `tools/scripts/ci/` são gerenciados pelo Groundwork: não os edite localmente; mudanças vão para o catálogo. Consulte [CI-CD.md](CI-CD.md).
- Dependências externas exigem licença permissiva, gratuita, versão fixada e manutenção verificada.
- Nunca sugira, instale ou adicione biblioteca paga, proprietária, source-available, com licença comercial obrigatória, copyleft ou outra restrição incompatível com a distribuição MIT do PixelPointer. Licença ausente, ambígua ou não verificada bloqueia a dependência.
- O padrão arquitetural Mediator é permitido quando houver necessidade comprovada de desacoplar dispatch, handlers e pipelines. Nesses casos, use `Nuuvify.CommonPack.Mediator` com versão centralizada e licença da versão exata verificada; nunca use MediatR.
- Não adicione broker, cache distribuído, microsserviço ou novo datastore sem ADR e evidência operacional.
- APIs públicas usam OpenAPI, `application/problem+json` e versionamento explícito.
- Mudanças de schema seguem expand-contract e devem ser testadas com banco real.
- Tabelas com potencial de crescimento elevado ou retenção temporal devem usar Table Partitioning/Declarative Partitioning por faixa de data, preferencialmente partições mensais; a solução deve definir criação antecipada, rollover, partição de segurança para datas fora da janela, índices por partição, retenção e testes de roteamento antes da produção.

## Qualidade mínima

- Código novo possui testes nos níveis adequados ao risco.
- Rode formatador, análise estática, build e testes do projeto alterado.
- Para fluxos do usuário, atualize ou adicione Playwright.
- Para banco de dados e filesystem, use testes de integração reais, não mocks como evidência principal.
- Imagens possuem build e smoke test.
- Superfícies privilegiadas recebem threat model e revisão de segurança.
- Reviews priorizam integridade de dados, segurança, regressões, concorrência e lacunas de teste.

## Instruções especializadas

As regras detalhadas em `.github/instructions/` são carregadas pelo caminho alterado ou pela descrição. Não replique essas regras em código ou documentação local.
