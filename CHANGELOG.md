# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.3](https://github.com/DevPossible/LCDPossible/compare/v0.1.2...v0.1.3) (2026-01-11)


### Bug Fixes

* **plugins:** correct package reference for Linux platform ([0e0cb79](https://github.com/DevPossible/LCDPossible/commit/0e0cb799b71c5130218c445779b94ceb400b057d))
* **tests:** exclude slow functional tests from smoke tests ([325fdd3](https://github.com/DevPossible/LCDPossible/commit/325fdd3c42b4c24b8d43c0318d3463d1e609fa0c))
* **video:** improve LibVLC initialization error handling and add install instructions ([8fdd14f](https://github.com/DevPossible/LCDPossible/commit/8fdd14f40bd602dd98c019a9ae6d77f02e98c67d))
* **web:** fix async void Dispose causing test hangs ([3b0a602](https://github.com/DevPossible/LCDPossible/commit/3b0a602e95902752c038e5dcb680b73bee0c3884))

## [Unreleased]

### Added

- **Page Effects System** - 30+ animated overlays for panels:
  - Background effects: aurora, snow, rain, bubbles, fireflies, stars-twinkle, lava-lamp, bokeh, smoke, waves, confetti, lightning, clouds, embers, fireworks, breathing-glow
  - Overlay effects: vhs-static, film-grain, lens-flare, neon-border, chromatic-aberration, crt-warp
  - Container animations: gentle-float, tilt-3d, bounce, wave
  - Character mascots: vanna-white, pixel-mascot, robot-assistant
  - Alert effects: spotlight
- **Effects Documentation** - New `docs/effects/README.md` with usage guide

### Changed

- Slowed down matrix-rain effect speed for better visibility
- Slowed down grid-pulse effect speed for smoother animation

### Removed

- Removed value-change triggered effects (particle-burst, glitch, glow-on-change, flip-digits, typewriter, slide-numbers, neon-trails, shake-on-warning, warning-flash, bounce-in)
- Removed color-cycle effect
- Removed red-man effect

## [0.3.0] - 2026-01-11

## [0.2.5] - 2026-01-11

## [0.2.4] - 2026-01-11

## [0.2.3] - 2026-01-11

## [0.2.2] - 2026-01-11

## [0.2.1] - 2026-01-11

## [0.2.0] - 2026-01-11

### Added

- **Screensavers Plugin** - 17 animated screensaver panels:
  - Classic: starfield, matrix-rain, bouncing-logo, mystify, plasma, fire
  - Nature: bubbles, rain
  - Abstract: spiral, clock, noise, warp-tunnel, pipes
  - Games: asteroids, missile-command, falling-blocks, game-of-life
- **Transition Effects** - 15 panel transition types:
  - fade, crossfade, slide (4 directions), wipe (4 directions)
  - zoom-in, zoom-out, push-left, push-right, random
- **Network Info Panel** - Display hostname and IP addresses
- **Thermal Panels** - CPU and GPU temperature gauges with color gradients
- **Video Plugin** - Play local videos, URLs, and YouTube links via LibVLC
- **Web Plugin** - Render HTML files and live websites via PuppeteerSharp
- **Images Plugin** - Animated GIF and image sequence playback
- **Hardware Monitoring** - Cross-platform system metrics:
  - Windows: LibreHardwareMonitor integration
  - Linux: /sys/class/hwmon and sysfs parsing
- **IPC Communication** - CLI-to-service communication for runtime control
- **Functional Tests** - Comprehensive CLI test suite (43 profile tests + panel tests)
- **Environment Variable Override** - `LCDPOSSIBLE_DATA_DIR` for portable/test mode
- **Panel Wildcards** - Pattern matching for panel selection (`cpu-*`, `*-graphic`)
- **`test` Command** - Render panels to JPEG files for verification
- **`profile` Commands** - Full profile management CLI:
  - new, list, show, delete, append-panel, remove-panel, move-panel
  - set-defaults, set-panelparam, get-panelparam, clear-panelparams
- **Debug Mode** - `--debug` flag for verbose diagnostics
- **Proxmox Demo Panels** - Demo mode when Proxmox not configured

### Changed

- Improved README documentation with complete panel reference
- Enhanced plugin metadata with help text and examples
- Optimized slideshow for single-panel mode
- Better error handling and caching for slideshow panels

### Fixed

- Thermal panel text positioning for better centering
- Plugin discovery in Release builds

## [0.1.2](https://github.com/DevPossible/LCDPossible/compare/v0.1.1...v0.1.2) (2026-01-10)

### Bug Fixes

- use bash shell for cross-platform builds, add manual dispatch ([f03c064](https://github.com/DevPossible/LCDPossible/commit/f03c064ef466c7a95f9090490e4878f56534ec19))

## [0.1.1](https://github.com/DevPossible/LCDPossible/compare/v0.1.0...v0.1.1) (2026-01-10)

### Features

- add complete CI/CD pipeline with Release Please ([fefca52](https://github.com/DevPossible/LCDPossible/commit/fefca5270db622f4edebd43729d68377773f655f))

## [0.1.0] - 2025-01-10

### Added 0.1.0

- Core LCD device abstraction with `ILcdDevice` interface
- USB HID communication layer using HidSharp
- Thermalright Trofeo Vision 360 ARGB driver (1280x480 LCD)
- PA120 Digital segment display driver (stub)
- JPEG and RGB565 image encoding
- System info display panels (CPU, GPU, RAM, Basic Info)
- Proxmox VE integration for server monitoring
- YAML-based display profile configuration
- CLI commands: `list`, `test`, `set-image`, `serve`, `show`
- Windows Service support via Microsoft.Extensions.Hosting.WindowsServices
- Serilog structured logging
