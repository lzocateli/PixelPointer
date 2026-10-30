---
name: "Documentação"
description: "Use ao criar ou alterar README, documentação técnica, runbook, ADR, plano ou contrato documentado."
applyTo: "**/*.md, docs/**, contracts/**/*.yaml, contracts/**/*.json, contracts/**/*.proto"
---

# Documentação

- Escreva em português brasileiro, com termos técnicos consistentes.
- Preserve comandos, identificadores e labels literais exigidos pelas ferramentas.
- O README raiz é autoridade para visão, escopo e princípios; `.github/` define procedimentos.
- Não duplique conteúdo extenso; use links entre documentos proprietários.
- ADRs vivem em `docs/adr`, usam `AAAA-NNNN-titulo.md` e começam como `proposed`.
- ADRs registram decisões duradouras; não substituem requisitos, tarefas ou autorização. Registre origem, revisor humano e referências.
- IA não marca ADR como `accepted` nem artefato como aprovado sem decisão humana explícita.
- README, arquitetura, segurança e ADRs aceitos restringem mudanças. Conflitos exigem decisão humana; não crie fonte paralela de requisitos em guias, prompts ou Issues.
- Runbooks incluem sinais, diagnóstico, mitigação, recuperação e escalonamento.
- Comandos informam diretório, pré-requisitos, resultado esperado e impacto.
- Exemplos usam valores fictícios; nunca inclua secret, host ou dado real.
- Registre limitações, matriz suportada, riscos e validações não executadas.
- Links locais devem resolver e headings devem ser únicos.
- Diferencie requisito, proposta, decisão aceita e comportamento implementado.