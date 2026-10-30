# Política de segurança

> **Configuração a verificar pelo mantenedor:** gerar este documento não
> habilita nem consulta o GitHub Private Vulnerability Reporting. Antes de
> divulgar um canal privado, confirme sua disponibilidade nas configurações
> do repositório. Não publique vulnerabilidades em Issues ou discussões.

## Versões suportadas

O mantenedor do PixelPointer deve registrar aqui a matriz de versões
suportadas, datas de fim de suporte e política de backports. Este template não
determina se existem releases nem promete suporte a qualquer versão. Até a
publicação dessa matriz, não presuma uma janela de suporte.

## Como reportar

Não abra issue pública para vulnerabilidades. Use
[Report a vulnerability](https://github.com/lzocateli/PixelPointer/security/advisories/new)
na aba **Security** do repositório. Se o recurso não estiver disponível, aguarde a
publicação de um canal privado oficial; não envie detalhes a endereços inferidos
nem por comentários públicos.

Inclua, quando possível:

- versão ou commit afetado;
- ambiente e sistema operacional;
- pré-condições e impacto;
- passos mínimos de reprodução;
- prova de conceito sanitizada;
- mitigação conhecida.

Não envie credenciais reais, chaves privadas, dados de terceiros ou dumps completos.

Não acesse dados de terceiros, não mantenha persistência e não execute testes
destrutivos fora de ambientes sob seu controle.

## Resposta esperada

- confirmação inicial em até 3 dias úteis;
- triagem e classificação em até 7 dias úteis;
- comunicação coordenada da correção conforme impacto e complexidade;
- crédito ao pesquisador quando solicitado e permitido.

Os prazos são objetivos operacionais, não garantia contratual.

## Prioridade

Recebem prioridade máxima vulnerabilidades que permitam:

- executar código ou comando sem autorização;
- contornar autenticação ou autorização;
- atravessar isolamento entre usuários, tenants ou ambientes;
- obter tokens, certificados, secrets ou dados protegidos;
- comprometer imagens, atualizações ou cadeia de suprimentos.

## Resposta coordenada

Correções incluem teste de regressão, análise de variantes e atualização de
dependências, imagens, SBOM e orientação de mitigação quando aplicável. A
divulgação ocorre depois da disponibilização da correção e de prazo razoável para
atualização, salvo exploração ativa que exija comunicação imediata.

Não há programa de recompensa declarado.
