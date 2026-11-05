# Duas contas corporativas no GitHub e no Copilot

**Português (Brasil)** · [English](en/contas-github-copilot.md) · [Español](es/contas-github-copilot.md)

Este tutorial orienta colaboradores a usar duas contas de organizações diferentes
sem confundir licença do Copilot, acesso ao repositório e autoria de commits.
Os exemplos são fictícios. Nenhum comando deste guia faz commit, push ou deploy.

## Pré-requisitos e limites

- VS Code atualizado, Git e GitHub CLI (`gh`) instalados.
- Acesso autorizado aos repositórios e ao Copilot em cada conta.
- Permissão da empresa para usar IA com o código do projeto.
- As instruções de CLI abaixo pressupõem duas contas em `github.com`.
  Contas em outros hosts exigem autenticação e seleção do host correspondente.
- Use o fluxo de autenticação aprovado pela empresa, inclusive SSO quando exigido.
  Não grave tokens em arquivos do repositório nem os inclua em comandos ou logs.

| Configuração | O que controla |
|---|---|
| Conta do Copilot no VS Code | Licença, consumo e políticas do Copilot |
| Conta do GitHub Pull Requests | Acesso da extensão a PRs e issues |
| Conta do Git/`gh` | Acesso a repositórios e operações remotas |
| `user.name` e `user.email` do Git | Autoria dos commits, não autenticação |

Uma configuração não troca automaticamente as outras. O proprietário do
repositório não seleciona a licença do Copilot; `gh auth status` não informa
qual conta o Copilot está usando.

## Selecionar o Copilot por projeto

No VS Code, com o projeto aberto:

1. Abra **Accounts / Contas** na barra de atividades.
2. Selecione **Manage Extension Account Preferences**.
3. Escolha **GitHub Copilot Chat**.
4. Selecione a conta autorizada pela organização responsável pelo código.
5. Se a conta não estiver disponível, adicione-a pelo fluxo de login do GitHub e
   repita a seleção. Confira o usuário no navegador antes de autorizar.

A preferência é salva por workspace e perfil. Repita no projeto da outra
organização, selecionando a outra conta. Confira a preferência selecionada antes
de enviar conteúdo ao chat ou habilitar sugestões.

Configure também a preferência de **GitHub Pull Requests**, se disponível na
lista, para a conta com acesso ao repositório. Essa escolha não altera o login do
terminal.

Para contas em `GHE.com`, siga a configuração específica de provedor de
autenticação na [documentação oficial do VS Code](https://code.visualstudio.com/docs/setup/copilot#_use-a-different-github-account-per-workspace-or-profile);
não presuma que o fluxo de `github.com` se aplica sem alterações.

## Separar perfis do VS Code

Em **Manage → Profiles → Create Profile**, crie perfis como **Empresa A** e
**Empresa B**. Abra cada workspace no perfil correspondente e selecione a conta
do Copilot conforme a seção anterior.

Perfis organizam configurações e extensões, mas não são uma barreira de
segurança e não isolam automaticamente credenciais do terminal.

## Conectar e alternar contas no GitHub CLI

Execute no terminal local, em qualquer diretório. Autentique cada conta pelo
navegador, repetindo o comando e conferindo o usuário autorizado:

```bash
gh auth login --hostname github.com --git-protocol https --web
```

O resultado esperado é a conta adicionada ao armazenamento de credenciais usado
pelo CLI. O login não altera a conta do Copilot. Para selecionar a conta ativa:

```bash
gh auth switch --hostname github.com --user USUARIO_EMPRESA_A
```

No trabalho da outra empresa, substitua por `USUARIO_EMPRESA_B`. Verifique a conta
ativa com `gh auth status`, sem publicar sua saída integral em tickets ou logs.

**A troca não é por projeto:** muda a conta ativa do `gh` naquele ambiente.
Evite alternar enquanto houver operações de outra conta em execução.

## Diferenciar HTTPS, SSH e autoria Git

Na raiz do clone, `git remote -v` mostra o transporte configurado; não publique
URLs que contenham credenciais.

- **HTTPS:** `gh auth setup-git` configura o Git para usar o helper de credenciais
  do `gh`. Isso modifica a configuração Git do usuário; execute somente se
  permitido pela empresa. Um helper ou regra por URL já existente pode influenciar
  a conta usada; confirme a configuração antes de operações remotas.
- **SSH:** `gh auth switch` não troca chaves SSH. Para duas identidades, use uma
  chave e um alias de host por conta, conforme a
  [orientação oficial do GitHub](https://docs.github.com/en/authentication/connecting-to-github-with-ssh).
  Configure cada remote com o alias adequado e não compartilhe chaves privadas.

### SSH com múltiplas contas e organizações

No arquivo de configuração SSH do usuário (Linux/macOS: `~/.ssh/config`;
Windows com OpenSSH: `%USERPROFILE%\.ssh\config`), adapte os blocos existentes,
sem sobrescrever outras entradas. Exemplo fictício:

```sshconfig
Host github.com
  HostName github.com
  User git
  IdentityFile ~/.ssh/id_ed25519_github
  IdentitiesOnly yes

Host github-pessoal
  HostName github.com
  User git
  IdentityFile ~/.ssh/id_ed25519_github_pessoal
  IdentitiesOnly yes

Host github-empresa-b
  HostName github.com
  User git
  IdentityFile ~/.ssh/id_ed25519_github_empresa_b
  IdentitiesOnly yes
```

No GitHub, o usuário SSH é sempre **`git`**, não o login da sua conta.
A chave pública cadastrada no GitHub determina a conta autenticada.
`Host` é o alias local que escolhe a configuração; `HostName` é o servidor real.
`IdentitiesOnly yes` limita as identidades oferecidas às configuradas, evitando
que o agente ofereça chaves não relacionadas. Outras entradas aplicáveis, inclusive
`Host *`, podem adicionar identidades; revise configurações sobrepostas.
Nunca coloque o conteúdo de uma chave privada neste arquivo.

Cadastre cada chave pública na conta correspondente e autorize-a para SSO na
organização, quando exigido. Uma conta que acessa várias organizações pode usar
a mesma chave autorizada: os aliases separam identidades, não concedem permissões.

No terminal local, em qualquer diretório, teste a identidade antes de operar:

```bash
ssh -T git@github-pessoal
ssh -T git@github-empresa-b
```

Na primeira conexão, confira a fingerprint do servidor com as
[fingerprints oficiais do GitHub](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/githubs-ssh-key-fingerprints)
antes de aceitar. O resultado esperado identifica o usuário: `Hi USERNAME! ...`.
O GitHub não oferece shell e pode retornar código `1` mesmo com autenticação
bem-sucedida; confira a mensagem, não apenas o código.

Para clonar com o alias pessoal, execute no diretório pai desejado:

```bash
git clone -b main git@github-pessoal:CONTA_EXEMPLO/REPOSITORIO_EXEMPLO.git
```

Não use `CONTA_EXEMPLO@github-pessoal:...`: isso substituiria `User git` pelo
nome da conta e impediria a autenticação esperada.
O proprietário após `:` pode ser uma conta ou organização; ele não escolhe
a identidade SSH.

Para trocar o remote de um clone existente, execute na raiz do clone, substituindo
os placeholders e preservando o proprietário e o repositório atuais:

```bash
git remote -v
git remote set-url origin git@github-pessoal:CONTA_EXEMPLO/REPOSITORIO_EXEMPLO.git
git remote -v
git ls-remote origin HEAD
```

`set-url` altera a URL local, sem push nem mudança de histórico. A listagem deve
mostrar o alias correto para fetch e push; `ls-remote` confirma acesso de leitura,
não permissão de escrita. Se houver uma URL de push separada, confira-a com
`git config --get-all remote.origin.pushurl` e atualize cada entrada necessária
com `git remote set-url --push origin NOVA_URL URL_ANTIGA`.
Para desfazer, restaure a URL anterior com `git remote set-url origin URL_ANTIGA`
e, se alteradas, as URLs de push.

### Autoria local dos commits

Para definir a autoria apenas neste repositório, execute na raiz do clone:

```bash
git config --local user.name "Seu Nome"
git config --local user.email "seu-email-corporativo@example.com"
```

Esses comandos alteram somente a configuração local do clone. Confira os valores
com `git config --local --get user.name` e
`git config --local --get user.email`. Eles não concedem acesso ao GitHub.

## Conferência antes de trabalhar

- Projeto A: Copilot A, acesso GitHub A e e-mail Git A.
- Projeto B: Copilot B, acesso GitHub B e e-mail Git B.
- Confirme a conta do Copilot nas preferências da extensão e a conta do CLI
  separadamente.
- Não use a licença de outra empresa para enviar código sem autorização.
- Não coloque credenciais nem seletores de identidade pessoal nas instruções
  versionadas do Copilot; a escolha ocorre na autenticação do editor.

Os nomes dos menus podem variar conforme a versão do VS Code. Este guia não
representa validação de duas contas corporativas reais ou de políticas específicas
de uma organização.
