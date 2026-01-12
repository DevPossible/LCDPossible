# Security Policy

## Supported Versions

We release patches for security vulnerabilities for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 0.8.x   | :white_check_mark: |
| < 0.8   | :x:                |

## Reporting a Vulnerability

We take the security of LCDPossible seriously. If you believe you have found a security vulnerability, please report it to us as described below.

**Please do not report security vulnerabilities through public GitHub issues.**

### How to Report

1. **GitHub Security Advisories**: Use the [GitHub Security Advisories](https://github.com/DevPossible/LCDPossible/security/advisories/new) feature (preferred)
2. **Email**: Contact the maintainers via GitHub discussions or create a private security advisory

### What to Include

Please include as much of the following information as possible:

- Type of issue (e.g., buffer overflow, SQL injection, cross-site scripting, etc.)
- Full paths of source file(s) related to the manifestation of the issue
- The location of the affected source code (tag/branch/commit or direct URL)
- Any special configuration required to reproduce the issue
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue, including how an attacker might exploit it

### Response Timeline

- We will acknowledge receipt of your vulnerability report within 3 business days
- We will send a more detailed response within 7 days indicating next steps
- We will keep you informed of the progress towards a fix
- We will notify you when the vulnerability is fixed

## Security Best Practices for Users

### Configuration Files

- **Never commit sensitive data** to version control:
  - Use environment variables or local config files (*.Local.json)
  - These are excluded by .gitignore: `*.env`, `secrets.json`, `appsettings.*.Local.json`

### Proxmox API Integration

- **Use API tokens** instead of username/password authentication
- **Create read-only tokens** with minimal required permissions
- **Use SSL/TLS** for API connections (disable `IgnoreSslErrors` only for testing with self-signed certificates)
- **Rotate tokens regularly** as part of good security hygiene

### File Permissions

- On Linux/macOS, ensure configuration files have appropriate permissions:
  ```bash
  chmod 600 /etc/lcdpossible/appsettings.json
  ```

### Running as a Service

- **Windows**: The service runs under LocalSystem by default. Consider using a dedicated service account
- **Linux**: Use systemd with a dedicated user account (not root)
- **macOS**: Use launchd with a dedicated user account

## Known Security Considerations

### USB Device Access

LCDPossible requires direct USB HID device access:
- **Linux**: Requires udev rules for unprivileged access (see installation documentation)
- **Windows**: Requires administrator privileges for first-time device access
- **macOS**: May require Security & Privacy settings adjustment

### LibVLC Integration

The video panel feature uses LibVLC for media playback:
- Keep LibVLC updated to receive security patches
- Be cautious when playing untrusted media files or streams

### Web Panel

The web panel uses PuppeteerSharp (headless browser):
- Be cautious when rendering untrusted web content
- Network-isolated environments are recommended for displaying external websites

## Secure Development

### For Contributors

- Never commit secrets or credentials to the repository
- Use the provided .gitignore patterns
- Mask sensitive data in logs and CLI output
- Validate and sanitize all external inputs
- Follow principle of least privilege for API integrations
- Use parameterized queries if database access is added in the future

### Code Review Checklist

- [ ] No hardcoded credentials or API keys
- [ ] Sensitive data properly masked in logs
- [ ] Input validation and sanitization
- [ ] Proper error handling (no information leakage)
- [ ] Dependencies checked for known vulnerabilities
- [ ] SSL/TLS used for network communication
- [ ] Proper resource disposal (IDisposable pattern)

## Security Updates

Security updates are released as soon as possible after a vulnerability is confirmed. We recommend:

- Subscribe to GitHub release notifications
- Enable Dependabot alerts for your fork
- Keep LCDPossible updated to the latest version

## Acknowledgments

We appreciate the security research community's efforts to help keep LCDPossible secure. Responsible disclosure helps protect all users.
