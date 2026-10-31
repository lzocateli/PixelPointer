---
name: "Documentação"
description: "Use ao criar ou alterar README, documentação técnica, runbook, ADR, plano ou contrato documentado."
applyTo: "**/*.md, docs/**, contracts/**/*.yaml, contracts/**/*.json, contracts/**/*.proto"
---

# Documentação multilíngue

- Escreva primeiro em português brasileiro (pt-BR), a fonte normativa do projeto.
- Sempre que criar ou alterar documentação autoral voltada a usuários,
	colaboradores ou operadores, crie/atualize as versões em inglês (en) e espanhol
	(es) na mesma mudança. A regra cobre todo o projeto: README, guias, planos,
	relatórios, runbooks, documentação de testes, contribuição e operação. Também
	vale quando uma mudança de código altera comportamento ou comandos documentados.
- Esta regra substitui o escopo de tradução mais restrito registrado em decisões
	anteriores: planos e relatórios também precisam de versões en/es; pt-BR continua
	sendo a referência para resolver divergências.
- Mapa de arquivos: `README.md` corresponde a `README.en.md` e `README.es.md`;
	`docs/<caminho>.md` corresponde a `docs/en/<caminho>.md` e
	`docs/es/<caminho>.md`. Preserve os pares legados `docs/uso.md`,
	`docs/en/usage.md` e `docs/es/uso.md`. Para documentação autoral fora da raiz e
	de `docs/`, use arquivos irmãos `<nome>.en.md` e `<nome>.es.md`.
- Se uma versão traduzida ainda não existir, crie-a na alteração que introduz ou
	modifica o documento. Mantenha links de navegação entre os idiomas e valide
	links relativos em cada versão.
- Traduza significado, contexto e limitações; não faça substituição mecânica de
	palavras. Não acrescente capacidades, suporte ou garantias ausentes da versão
	normativa. Atualize as três versões juntas quando comandos, exemplos, opções,
	pré-requisitos ou resultados esperados mudarem.
- Exceções: textos de licença e avisos de terceiros devem permanecer fiéis ao
	original; changelogs gerados devem seguir sua ferramenta; arquivos de dados,
	metadados e instruções de agente/MCP não são documentação de produto a traduzir.
	Preserve nesses arquivos o idioma e a sintaxe exigidos pelo consumidor.
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
