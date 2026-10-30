# Convenção de commits

Use [Conventional Commits 1.0.0](https://www.conventionalcommits.org/pt-br/v1.0.0/).

```text
<tipo>[escopo opcional][!]: <descrição>
```

A descrição é em português brasileiro, no imperativo, começa com minúscula e não termina com ponto.

## Tipos

| Tipo | Uso |
| --- | --- |
| `feat` | comportamento novo ou ampliado |
| `fix` | correção de defeito ou vulnerabilidade |
| `docs` | somente documentação |
| `style` | somente formatação |
| `refactor` | reorganização sem alteração funcional |
| `perf` | melhoria de desempenho |
| `test` | testes sem alteração de produção |
| `build` | build, dependências e imagens |
| `ci` | workflows e automação |
| `chore` | manutenção restante |
| `revert` | reversão formal |

## Escopos

`app`, `core`, `rendering`, `capture`, `hotkeys`, `overlay`, `ipc`, `platform`, `linux`, `windows`, `macos`, `packaging`, `tests`, `docs`, `tooling`, `deps`, `actions`, `repo`.

Exemplos:

```text
feat: adicione exportação do relatório
fix: impeça repetição de comando expirado
docs: registre limites da integração
```

Mudança incompatível usa `!` e rodapé com impacto e migração:

```text
feat!: exija revisão monotônica no inventário

BREAKING CHANGE: clientes antigos devem atualizar antes do servidor.
```