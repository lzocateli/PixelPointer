# ADR 2026-0001: Atualização opt-in com proveniência Sigstore verificada no aplicativo

**Português (Brasil)** · [English](../en/adr/2026-0001-atualizacao-opt-in.md) · [Español](../es/adr/2026-0001-atualizacao-opt-in.md)

**Status:** proposed

**Data:** 2026-11-04  
**Responsáveis:** Lincoln Zocateli  
**Origem:** IA assistida; opções escolhidas pelo solicitante na sessão de 2026-11-04  
**Revisor humano:** pendente  
**Relacionado:** [plano de atualização](../plano-atualizacao.md), IMPL-06 do [plano de implementação](../plano-implementacao.md#8-pendências-consolidadas)

## Contexto

O [plano de atualização](../plano-atualizacao.md) deixou seis decisões em aberto:
periodicidade, fonte de metadados, prova de proveniência em runtime sem `gh`,
identificação da origem da instalação, interface e momento da aplicação. As
releases já publicam `SHA256SUMS`, SBOM e atestações Sigstore keyless; os
instaladores próprios conferem só o SHA-256. O aplicativo é Native AOT, sem
reflexão, e o `settings.json` é somente leitura para o aplicativo.

## Forças de decisão

- não instalar conteúdo sem prova de origem nem substituir canais de terceiros;
- não pedir nem contornar elevação e não interromper apresentações;
- nenhuma dependência nova, compatibilidade com AOT e nenhum secret novo no CI;
- operação reversível e falha fechada.

## Opções consideradas

### Proveniência

- **Verificação nativa do bundle Sigstore (escolhida):** reaproveita as atestações
  existentes, sem chave nem secret novo; exige embutir o material de confiança
  público e acompanhar rotações.
- **Chave ECDSA própria assinando o `SHA256SUMS`:** verificação simples, mas cria
  secret, rotação e runbook novos.
- **Adiar a instalação:** sem risco de instalação, mas não atende ao objetivo.

### Origem da instalação

- **Recibo dos instaladores e checagens do filesystem (escolhida):** o recibo sozinho
  não autoriza nada; é combinado com escrita do usuário atual, ausência de escrita
  de grupo ou outros, processo não elevado e exclusão de caminhos do `.deb`,
  Homebrew e winget.
- **Somente heurística de caminhos:** não distingue instalação portátil.

## Decisão

1. **Periodicidade:** somente no daemon, com verificação automática ativada: 5
   minutos após iniciar e depois a cada 24 horas; 1 hora após falha. A
   verificação manual ignora o intervalo.
2. **Metadados:** `GET /repos/lzocateli/PixelPointer/releases/latest` serve só para
   descobrir a tag estável. O pacote vem de `releases/download/v<versão>/` e é
   aceito apenas com o tamanho publicado, o SHA-256 do `SHA256SUMS` e a
   atestação; a versão também é confirmada executando `--version` no binário
   atestado, o que impede servir outro binário legítimo como a versão anunciada.
3. **Proveniência:** verificação em C# com a BCL do bundle obtido em
   `GET /repos/.../attestations/sha256:<digest>`: cadeia Fulcio, SCT do log CT,
   SET e prova de inclusão Merkle com checkpoint assinado do Rekor v1, assinatura
   DSSE, corpo do log ligado ao envelope e ao certificado, statement in-toto SLSA
   v1 cobrindo nome e digest do pacote, e identidade: emissor
   `token.actions.githubusercontent.com`, repositório
   `https://github.com/lzocateli/PixelPointer` com IDs numéricos `1439215418`
   (repositório) e `16601706` (dono), runner `github-hosted`, signatário
   `.github/workflows/gw-github-assets.yml` em `refs/heads/main` ou `refs/tags/v*`.
4. **Origem:** `install.sh` e `install.ps1` gravam `install-receipt.json`
   (`installer`, `scope` user/global, `rid`). Somente escopo de usuário com as
   checagens acima pode ser substituído; os demais canais só avisam com o comando
   do gerenciador.
5. **Interface:** CLI `pixelpointer update` e submenu **Atualizações** na tray;
   preferências em `update.json`, gravado pelo aplicativo por troca atômica, com
   recusa em sobrescrever arquivo inválido ou alterado concorrentemente.
6. **Aplicação:** ao encerrar o daemon pelo usuário (tray, `quit`, Ctrl+C) e por
   `pixelpointer update apply`/`install` com o daemon parado. Linux e macOS usam
   troca atômica de diretórios (`renameat2`/`renamex_np`, com fallback por duas
   renomeações e rollback); no Windows, a versão preparada conclui a troca depois
   que o processo anterior encerra. Sem reinício automático.

Revisar esta decisão se o GitHub migrar as atestações para o Rekor v2 ou para
outro emissor, se o workflow de release mudar ou se um canal preview for pedido.

## Consequências

### Positivas

- Instalações de usuário atualizam sem `gh`, sem elevação e sem scripts remotos.
- Nenhuma dependência, chave ou secret novo.

### Negativas e trade-offs

- Material de confiança do Sigstore e identidade do workflow ficam embutidos:
  rotação, Rekor v2 (Ed25519, sem suporte na BCL) ou mudança do workflow fazem a
  verificação falhar fechada até atualizar pelo instalador.
- A API anônima do GitHub tem limite por IP.
- Instalações anteriores ao recibo precisam reexecutar o instalador uma vez.

## Segurança e privacidade

Nada é executado antes de SHA-256 e proveniência serem aceitos; extração só de
arquivos regulares esperados; staging no mesmo volume, em diretório privado;
processos auxiliares nunca elevados e limitados ao diretório derivado do próprio
executável e do marcador. Sem telemetria: apenas User-Agent fixo.

## Dados, protocolo e compatibilidade

Novos arquivos: `install-receipt.json` (versão 1), `update.json` (versão 1) e
`<pai>/.pixelpointer.update/staged.json`. Instaladores continuam compatíveis com
instalações existentes.

## Observabilidade e operação

Eventos `UPDATE` no log de diagnóstico (sem conteúdo capturado), status na tray e
em `pixelpointer update status`, resultado da última aplicação na próxima abertura
do daemon.

## Validação

- Testes unitários com o bundle real da v0.3.1 e casos adulterados (payload, SET,
  prova, checkpoint, log, identidade, digest).
- Testes de filesystem real para preferências, origem, extração, staging,
  aplicação, rollback e limpeza.
- E2E em container Ubuntu 24.04 com binário 0.3.0 atualizado para a release
  publicada a partir do GitHub real; instalação global recusada.
- Pendentes: Windows (auxiliar de troca, `install.ps1`) e macOS (`renamex_np`).

## Rollout e rollback

Opt-in e desativado por padrão. Para desligar: `pixelpointer update auto-check off`.
Para reverter a funcionalidade, uma release sem o comando mantém instalações
funcionais; uma atualização com falha restaura a versão anterior automaticamente.

## Referências

- [Plano de atualização](../plano-atualizacao.md) e [guia de uso](../uso.md#atualizar)
- [Sigstore trusted root](https://github.com/sigstore/root-signing/blob/main/targets/trusted_root.json)
- [RFC 6962](https://www.rfc-editor.org/rfc/rfc6962) e [DSSE](https://github.com/secure-systems-lab/dsse)
