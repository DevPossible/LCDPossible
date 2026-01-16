# Proxmox VE Panels

Proxmox VE cluster monitoring panels for LCDPossible.

## Quick Reference

```bash
# Show cluster overview
lcdpossible show proxmox-summary

# Show VM/container list
lcdpossible show proxmox-vms
```

## Configuration

Configure Proxmox connection in `display-profile.yaml`:

```yaml
proxmox:
  host: https://your-proxmox-host:8006
  tokenId: your-token-id
  tokenSecret: your-token-secret
  # OR use username/password:
  # username: root@pam
  # password: your-password
```

## Panels

| Panel | Description | Category |
|-------|-------------|----------|
| [Proxmox Summary](panels/proxmox-summary/proxmox-summary.md) | Proxmox cluster overview with node status and resource usage | Proxmox |
| [Proxmox VMs](panels/proxmox-vms/proxmox-vms.md) | List of VMs and containers with status and resource usage | Proxmox |

