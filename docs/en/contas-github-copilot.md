# Two corporate accounts in GitHub and Copilot

[Português (Brasil)](../contas-github-copilot.md) · **English** · [Español](../es/contas-github-copilot.md)

This tutorial helps contributors use accounts from two different organizations
without confusing Copilot licensing, repository access, and commit authorship.
Examples are fictitious. No command in this guide commits, pushes, or deploys.

## Prerequisites and limitations

- An up-to-date VS Code installation, Git, and GitHub CLI (`gh`).
- Authorized repository and Copilot access for each account.
- Company permission to use AI with the project's code.
- The CLI instructions below assume two accounts on `github.com`.
  Accounts on other hosts require authentication and selection of that host.
- Use the company's approved authentication flow, including SSO when required.
  Never store tokens in repository files or include them in commands or logs.

| Configuration | What it controls |
|---|---|
| Copilot account in VS Code | Copilot license, usage, and policies |
| GitHub Pull Requests account | Extension access to PRs and issues |
| Git/`gh` account | Repository access and remote operations |
| Git `user.name` and `user.email` | Commit authorship, not authentication |

Changing one does not automatically change the others. The repository owner does
not select the Copilot license; `gh auth status` does not report which account
Copilot is using.

## Select Copilot per project

In VS Code, with the project open:

1. Open **Accounts** in the Activity Bar.
2. Select **Manage Extension Account Preferences**.
3. Choose **GitHub Copilot Chat**.
4. Select the account authorized by the organization responsible for the code.
5. If the account is unavailable, add it through the GitHub sign-in flow and
   repeat the selection. Check the browser's signed-in user before authorizing.

The preference is saved per workspace and profile. Repeat for the other
organization's project, selecting the other account. Check the selected
preference before sending content to chat or enabling suggestions.

Also configure the **GitHub Pull Requests** account preference, if listed, for
the account with repository access. This does not change terminal authentication.

For `GHE.com` accounts, follow the authentication-provider configuration in the
[official VS Code documentation](https://code.visualstudio.com/docs/setup/copilot#_use-a-different-github-account-per-workspace-or-profile);
do not assume the `github.com` flow applies unchanged.

## Separate VS Code profiles

Under **Manage → Profiles → Create Profile**, create profiles such as
**Company A** and **Company B**. Open each workspace in its corresponding profile
and select the Copilot account as described above.

Profiles organize settings and extensions but are not a security boundary and do
not automatically isolate terminal credentials.

## Connect and switch GitHub CLI accounts

Run in a local terminal, from any directory. Authenticate each account in the
browser, repeating the command and checking which user is authorized:

```bash
gh auth login --hostname github.com --git-protocol https --web
```

The expected result is an account added to the CLI's credential storage.
Signing in does not change the Copilot account. To select the active account:

```bash
gh auth switch --hostname github.com --user USUARIO_EMPRESA_A
```

For the other company, replace the placeholder with `USUARIO_EMPRESA_B`. Check
the active account with `gh auth status`, without posting its full output in
tickets or logs.

**Switching is not project-specific:** it changes the active `gh` account in that
environment. Avoid switching while another account's operations are running.

## Distinguish HTTPS, SSH, and Git authorship

At the clone root, `git remote -v` shows the configured transport; do not publish
URLs containing credentials.

- **HTTPS:** `gh auth setup-git` configures Git to use the `gh` credential helper.
  This modifies user-level Git configuration; run it only if company policy
  permits. An existing helper or URL-specific rule may affect account selection;
  check the configuration before remote operations.
- **SSH:** `gh auth switch` does not switch SSH keys. For two identities, use one
  key and one host alias per account, following the
  [official GitHub guidance](https://docs.github.com/en/authentication/connecting-to-github-with-ssh).
  Configure each remote with the appropriate alias and never share private keys.

### SSH with multiple accounts and organizations

In the user's SSH configuration file (Linux/macOS: `~/.ssh/config`;
Windows with OpenSSH: `%USERPROFILE%\.ssh\config`), adapt existing blocks without
overwriting other entries. Fictitious example:

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

On GitHub, the SSH user is always **`git`**, not your account login.
The public key registered with GitHub determines the authenticated account.
`Host` is the local alias selecting the configuration; `HostName` is the actual
server. `IdentitiesOnly yes` restricts offered identities to configured ones,
preventing the agent from offering unrelated keys. Other matching entries,
including `Host *`, may add identities; review overlapping configuration.
Never put private key contents in this file.

Register each public key with its corresponding account and authorize it for
organization SSO when required. An account accessing multiple organizations can
use the same authorized key: aliases separate identities, not grant permissions.

In a local terminal, from any directory, test the identity before operating:

```bash
ssh -T git@github-pessoal
ssh -T git@github-empresa-b
```

On the first connection, compare the server fingerprint with
[GitHub's official fingerprints](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/githubs-ssh-key-fingerprints)
before accepting. The expected response identifies the user: `Hi USERNAME! ...`.
GitHub provides no shell and may return exit code `1` even after successful
authentication; check the message, not just the code.

To clone using the personal alias, run in the desired parent directory:

```bash
git clone -b main git@github-pessoal:CONTA_EXEMPLO/REPOSITORIO_EXEMPLO.git
```

Do not use `CONTA_EXEMPLO@github-pessoal:...`: that would override `User git`
with the account name and prevent the expected authentication.
The owner after `:` can be an account or organization; it does not select
the SSH identity.

To change an existing clone's remote, run at the clone root, replacing placeholders
and preserving the current owner and repository:

```bash
git remote -v
git remote set-url origin git@github-pessoal:CONTA_EXEMPLO/REPOSITORIO_EXEMPLO.git
git remote -v
git ls-remote origin HEAD
```

`set-url` changes the local URL without pushing or changing history. The listing
should show the correct alias for fetch and push; `ls-remote` verifies read access,
not write permission. If there is a separate push URL, check it with
`git config --get-all remote.origin.pushurl` and update each necessary entry
with `git remote set-url --push origin NEW_URL OLD_URL`.
To undo, restore the previous URL with `git remote set-url origin OLD_URL`
and restore any push URLs you changed.

### Local commit authorship

To set authorship only for this repository, run at the clone root:

```bash
git config --local user.name "Your Name"
git config --local user.email "your-corporate-email@example.com"
```

These commands only change the clone's local configuration. Check the values with
`git config --local --get user.name` and
`git config --local --get user.email`. They do not grant GitHub access.

## Before starting work

- Project A: Copilot A, GitHub access A, and Git email A.
- Project B: Copilot B, GitHub access B, and Git email B.
- Check the Copilot account in extension preferences and the CLI account
  separately.
- Never use another company's license to submit code without authorization.
- Do not put credentials or personal identity selectors in versioned Copilot
  instructions; account selection belongs to editor authentication.

Menu names may vary with the VS Code version. This guide does not represent
validation with two real corporate accounts or organization-specific policies.
