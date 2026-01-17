# Third-Party Notices

LCDPossible uses third-party libraries under various open source licenses. This document provides attribution and license information for these dependencies.

## LibVLC / LibVLCSharp

**License:** LGPL 2.1+

LibVLC is the multimedia engine powering the video panel functionality. LibVLCSharp provides .NET bindings to LibVLC.

- LibVLC: https://www.videolan.org/vlc/libvlc.html
- LibVLCSharp: https://github.com/videolan/libvlcsharp
- License: https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html

**LGPL Compliance Note:** LCDPossible dynamically links to LibVLC at runtime. On Windows, the LibVLC binaries are provided via the `VideoLAN.LibVLC.Windows` NuGet package. On Linux and macOS, users must install LibVLC via their system package manager (e.g., `apt install vlc`, `brew install vlc`). Users may replace the LibVLC libraries with their own modified versions in accordance with the LGPL.

## HidSharp

**License:** Apache License 2.0

USB HID communication library.

- Repository: https://github.com/IntergatedCircuits/HidSharp
- License: https://www.apache.org/licenses/LICENSE-2.0

## SixLabors.ImageSharp

**License:** Six Labors Split License

Image processing library used for rendering and encoding frames.

- Repository: https://github.com/SixLabors/ImageSharp
- License: https://github.com/SixLabors/ImageSharp/blob/main/LICENSE

## YoutubeExplode

**License:** LGPL 3.0

YouTube video stream URL extraction for video panel playback.

- Repository: https://github.com/Tyrrrz/YoutubeExplode
- License: https://www.gnu.org/licenses/lgpl-3.0.html

## PuppeteerSharp

**License:** MIT

Headless browser automation for HTML/Web panel rendering.

- Repository: https://github.com/hardkoded/puppeteer-sharp
- License: https://opensource.org/licenses/MIT

## Scriban

**License:** BSD 2-Clause

Template engine for HTML panel rendering.

- Repository: https://github.com/scriban/scriban
- License: https://opensource.org/licenses/BSD-2-Clause

## LibreHardwareMonitorLib

**License:** Mozilla Public License 2.0

Hardware monitoring library for CPU, GPU, RAM, and other system metrics on Windows.

- Repository: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
- License: https://www.mozilla.org/en-US/MPL/2.0/

## Microsoft.Extensions.* Libraries

**License:** MIT

Various Microsoft .NET extension libraries for dependency injection, hosting, configuration, and logging.

- Repository: https://github.com/dotnet/runtime
- License: https://opensource.org/licenses/MIT

## Avalonia

**License:** MIT

Cross-platform UI framework used for the VirtualLcd simulator.

- Repository: https://github.com/AvaloniaUI/Avalonia
- License: https://opensource.org/licenses/MIT

---

For the complete license text of each dependency, please refer to the links provided above or the license files included in the respective NuGet packages.
