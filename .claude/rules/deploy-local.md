# deploy-local.ps1 Execution Rule

**IMPORTANT:** The `scripts/deploy-local.ps1` script must NOT be run from Git Bash or via `pwsh -c` from a bash shell.

## Why

The deploy script uses SSH/SCP which requires proper Windows SSH agent integration. When run from Git Bash or via `pwsh -c`, the SSH authentication fails with "Permission denied" errors because the SSH agent context is not properly inherited.

## Correct Usage

Run the script directly from a Windows PowerShell terminal:

```powershell
# Open Windows Terminal or PowerShell directly, then:
./scripts/deploy-local.ps1 -TargetHost cosmos -Distro proxmox
```

## Incorrect Usage (Don't Do This)

```bash
# From Git Bash - WILL FAIL
pwsh -c "./scripts/deploy-local.ps1 -TargetHost cosmos -Distro proxmox"

# From Claude Code Bash tool - WILL FAIL
Bash: pwsh -c "./scripts/deploy-local.ps1 ..."
```

## Alternative for Claude Code

When deployment is needed, instruct the user to:
1. Build the package: `./package.ps1 -SkipTests -NonInteractive`
2. Deploy manually from their PowerShell terminal
3. Or provide manual scp/ssh commands they can run
