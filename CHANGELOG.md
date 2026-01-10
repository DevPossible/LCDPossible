# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1](https://github.com/DevPossible/LCDPossible/compare/v0.1.0...v0.1.1) (2026-01-10)


### Features

* add complete CI/CD pipeline with Release Please ([fefca52](https://github.com/DevPossible/LCDPossible/commit/fefca5270db622f4edebd43729d68377773f655f))

## [Unreleased]

## [0.1.0] - 2025-01-10

### Added

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
