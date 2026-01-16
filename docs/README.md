# LCDPossible Documentation

Welcome to the LCDPossible documentation. LCDPossible is a cross-platform LCD controller service for HID-based LCD screens.

## Quick Links

| Section | Description |
|---------|-------------|
| [Getting Started](getting-started.md) | Installation and first steps |
| [Panels](panels/README.md) | Available display panels |
| [Effects](effects/README.md) | Page effects and animations |
| [Themes](themes/README.md) | Color themes and customization |
| [Configuration](configuration/README.md) | Profiles, settings, and service setup |
| [CLI Reference](cli/README.md) | Command-line interface |
| [Plugin Development](plugins/README.md) | Creating custom panels and device drivers |
| [Troubleshooting](troubleshooting.md) | Common issues and solutions |

## Documentation Structure

```
docs/
├── getting-started.md       # Installation & quick start
├── configuration/           # Profiles, settings, service setup
├── cli/                     # Command-line reference
├── panels/                  # Panel documentation
│   ├── system/              # CPU, GPU, RAM, Network panels
│   ├── media/               # Video, GIF, Images, Web panels
│   ├── screensavers/        # Screensaver panels
│   └── integrations/        # Proxmox and other integrations
├── effects/                 # Page effects
├── themes/                  # Color themes
├── plugins/                 # Plugin development guide
├── reference/               # Technical reference
│   ├── devices/             # USB device protocols
│   └── architecture.md      # System architecture
├── troubleshooting.md       # Common issues
└── examples/                # Example configurations
```

## For Users

- **New to LCDPossible?** Start with [Getting Started](getting-started.md)
- **Want to customize your display?** See [Panels](panels/README.md), [Effects](effects/README.md), and [Themes](themes/README.md)
- **Setting up as a service?** Check [Configuration](configuration/README.md)

## For Developers

- **Creating custom panels?** See [Plugin Development](plugins/README.md)
- **Adding device support?** See [Creating Device Drivers](plugins/creating-devices.md)
- **Understanding the codebase?** See [Architecture](reference/architecture.md)

## External Links

- [GitHub Repository](https://github.com/DevPossible/lcd-possible)
- [Releases](https://github.com/DevPossible/lcd-possible/releases)
- [Issue Tracker](https://github.com/DevPossible/lcd-possible/issues)

---

*[LCDPossible](https://github.com/DevPossible/lcd-possible) - Open source LCD controller*
