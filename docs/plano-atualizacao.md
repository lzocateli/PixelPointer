# Plano de atualização do PixelPointer

**Português (Brasil)** · [English](en/plano-atualizacao.md) · [Español](es/plano-atualizacao.md)

| Campo | Estado |
| --- | --- |
| Status do plano | **Implementado** em 2026-11-04; aguarda revisão humana e validação em Windows e macOS |
| Status da funcionalidade | Implementada: `pixelpointer update` e submenu **Atualizações** da tray; troca validada de ponta a ponta somente no Linux x64 |
| Rastreamento oficial | [IMPL-06 na seção 8 do plano de implementação](plano-implementacao.md#8-pendências-consolidadas) |
| Data | 2026-11-04 |
| Origem | Proposta do solicitante, detalhada com assistência de IA |
| Decisões técnicas | Escolhidas pelo solicitante em 2026-11-04 e registradas no [ADR 2026-0001](adr/2026-0001-atualizacao-opt-in.md) (`proposed`) |

## 1. Objetivo e estado atual

Definir uma atualização opt-in para instalações autogerenciadas do PixelPointer,
sem interromper uma apresentação e sem contornar as permissões do sistema. O
[README](../README.md#instalação) e o [guia de releases](release.md#canais-de-distribuição)
descrevem o comportamento atual: os instaladores próprios podem ser executados
novamente; `.deb`, Homebrew e winget são atualizados por seus respectivos
fluxos. Até esta implementação, o aplicativo não verificava nem instalava
atualizações; o comportamento implementado está no
[guia de uso](uso.md#atualizar).

Este documento propõe requisitos e sequência de trabalho. O status de execução
fica somente no item IMPL-06 do [plano principal](plano-implementacao.md#8-pendências-consolidadas).

## 2. Comportamento proposto

Preferências visíveis ao usuário:

| Opção | Padrão | Comportamento |
| --- | --- | --- |
| `Verificar atualizações automaticamente` | Desativada | Consulta periodicamente a existência de versão compatível e informa o usuário. A verificação manual continua disponível. |
| `Instalar atualizações automaticamente` | Desativada | Quando ativada junto da verificação automática, baixa, valida e prepara a atualização sem confirmação adicional. A preferência não autoriza interromper uma sessão ativa. |

A instalação automática depende da verificação automática: desativar a primeira
opção suspende também a instalação automática. A preferência de instalação deve
ficar indisponível ou ser desativada enquanto a verificação estiver desligada.

Regras por origem da instalação:

- Instalações autogerenciadas por usuário, feitas por `install.sh` ou
  `install.ps1`, podem receber a atualização automática quando o diretório for
  gravável pelo usuário atual e a origem puder ser identificada com confiança.
- `.deb`, Homebrew e winget podem verificar e avisar sobre uma versão nova, mas
  não podem instalar automaticamente. A mensagem deve indicar o fluxo do
  gerenciador correspondente, descrito no [guia de releases](release.md#canais-de-distribuição).
- Instalações globais, diretórios que exijam elevação, origem desconhecida ou
  instalação portátil não registrada não podem ser substituídas pelo atualizador.
  Não solicitar nem contornar elevação.
- O canal estável é o padrão. Previews só são consultadas se houver escolha
  explícita de canal; não misturar versões preview e estáveis.

## 3. Fluxo de atualização automática

1. Consultar metadados de release por HTTPS, respeitando canal, plataforma,
   arquitetura e uma frequência limitada. A verificação manual ignora o período
   de espera. A consulta não envia identificadores pessoais nem telemetria.
2. Se houver versão compatível, informar versão instalada e disponível. Com
   instalação automática habilitada, obter o pacote correspondente à origem
   autogerenciada; não baixar nem executar scripts de instalação remotos.
3. Baixar para uma área temporária restrita e validar versão, RID, tamanho,
   integridade e proveniência antes de alterar a instalação. O mecanismo de
   autenticação da proveniência além do `SHA256SUMS` precisa ser definido antes
   da implementação; uma soma de verificação não autenticada, isoladamente, não
   basta como prova de origem.
4. Preparar os arquivos em diretório de staging no mesmo volume da instalação,
   executar `--version` e `selftest` na versão nova e manter a versão corrente
   intacta caso qualquer etapa falhe.
5. Aplicar a troca somente após o daemon e as janelas terminarem naturalmente,
   sem overlay, gravação ou outra sessão ativa. Se for necessário um processo
   auxiliar para esperar o encerramento, ele deve ser específico da plataforma,
   limitado ao diretório autorizado e não elevado.
6. Efetuar a troca com recuperação da versão anterior se a instalação falhar.
   Não reiniciar o aplicativo automaticamente; a próxima abertura usa a versão
   atualizada e pode informar o resultado.

Se uma atualização estiver baixada, mas ainda não puder ser aplicada, informar
que será concluída ao encerrar o PixelPointer. Se a validação, a troca ou a
recuperação falhar, preservar os arquivos existentes e apresentar instrução
acionável; nunca apagar a versão anterior para liberar espaço antes da confirmação.

## 4. Etapas propostas

1. **Fechar requisitos e decisões:** escolher periodicidade, fonte de metadados,
   experiência de preferências, política de canal e validação de proveniência.
2. **Identificar instalação e preferências:** definir metadados seguros para
   distinguir instalação por usuário, gerenciador de pacotes, instalação global
   e origem desconhecida; persistir preferências por usuário sem guardar segredos.
3. **Implementar verificação:** descoberta de versão compatível, verificação
   manual e automática, avisos e supressão de consultas repetidas.
4. **Implementar instalação em staging:** download, validação, selftest,
   aplicação adiada, recuperação e mensagens de estado para Windows, Linux e
   macOS, respeitando os canais que permitem atualização própria.
5. **Integrar e documentar:** atualizar a interface disponível no produto,
   instaladores, documentação de instalação/releases e suporte de cada canal.

## 5. Critérios de conclusão

- As duas preferências persistem por usuário, começam desativadas e obedecem à
  dependência entre verificação e instalação automática.
- Verificação manual funciona com a verificação automática desativada; versões
  incompatíveis com canal, SO ou arquitetura são ignoradas.
- Atualizações inválidas, interrompidas, sem proveniência aceita ou sem permissão
  não alteram a instalação atual nem iniciam execução de conteúdo não validado.
- Atualização automática não interrompe overlay, gravação ou daemon em uso, não
  pede elevação e preserva a versão anterior em falha.
- `.deb`, Homebrew e winget nunca são substituídos pelo atualizador; o usuário
  recebe orientação correspondente ao canal detectado.
- Testes cobrem política de preferências, identificação de origem, integridade,
  staging, adiamento, falha/rollback e notificações, além da matriz por SO e
  arquitetura suportados. Testes que executem a aplicação seguem as regras de
  containers e nginx do repositório.

## 6. Decisões

As decisões abaixo foram escolhidas pelo solicitante em 2026-11-04 e estão
detalhadas no [ADR 2026-0001](adr/2026-0001-atualizacao-opt-in.md), ainda
`proposed` até revisão humana registrada.

- **Periodicidade:** somente no daemon; 5 minutos após iniciar e depois a cada
  24 horas (1 hora após falha).
- **Metadados:** `releases/latest` só descobre a tag estável; a identidade vem dos
  assets da tag, do `SHA256SUMS`, da atestação e do `--version` do binário atestado.
- **Proveniência:** verificação nativa do bundle Sigstore da atestação do GitHub,
  sem `gh` e sem dependência nova.
- **Origem:** recibo `install-receipt.json` gravado pelos instaladores, combinado com
  checagens de escrita, permissões e privilégio do processo.
- **Interface:** CLI `pixelpointer update` e submenu **Atualizações** da tray;
  preferências em `update.json`.
- **Aplicação:** ao encerrar o daemon pelo usuário e por `pixelpointer update apply`
  com o daemon parado; sem reinício automático.
- **Canal preview:** não oferecido nesta versão; somente estáveis são consultadas.

## 7. Fora de escopo

- Atualização silenciosa de `.deb`, Homebrew, winget ou instalações que exijam
  elevação.
- Reinício forçado, downgrade automático, migração destrutiva de dados ou
  execução de scripts obtidos da rede.
- Telemetria, coleta de identificadores pessoais e atualização automática de
  dependências de build.
