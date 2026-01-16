# Video Panels

Video playback panels for LCDPossible (supports local files, URLs, and YouTube).

## Quick Reference

```bash
# Play local video file
lcdpossible show video:path/to/video.mp4

# Play video from URL
lcdpossible show video:https://example.com/video.mp4

# Play YouTube video
lcdpossible show video:https://www.youtube.com/watch?v=VIDEO_ID
```

## Requirements

**Linux/macOS:** LibVLC must be installed via system package manager:
```bash
# Debian/Ubuntu
sudo apt install vlc libvlc-dev

# macOS
brew install vlc
```

**Windows:** LibVLC binaries are included automatically via NuGet.

## Panels

| Panel | Description | Category |
|-------|-------------|----------|
| [Video](panels/video/video.md) | Plays video files, streaming URLs, or YouTube links | Media |

