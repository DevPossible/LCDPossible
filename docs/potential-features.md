# Potential Features

Ideas and enhancements for future development.



## 3D Scene Animation Panel

Display simple 3D animations using a web page panel with JavaScript 3D libraries, driven by a JSON scene description file.

**Concept:**
- JSON file describes 3D objects, materials, lighting, camera, and animation keyframes
- Web panel loads a bundled HTML/JS renderer that parses the JSON
- Renders and animates the scene in real-time using WebGL

**Possible JS libraries:**
- Three.js - Full-featured 3D library
- Babylon.js - Game-focused 3D engine
- A-Frame - Declarative 3D/VR framework
- Zdog - Pseudo-3D for simple shapes (lightweight)

**JSON scene format example:**
```json
{
  "camera": { "position": [0, 2, 5], "lookAt": [0, 0, 0] },
  "lights": [
    { "type": "ambient", "color": "#404040" },
    { "type": "directional", "position": [5, 10, 7], "color": "#ffffff" }
  ],
  "objects": [
    { "type": "cube", "position": [0, 0, 0], "size": 1, "color": "#00ff00" }
  ],
  "animations": [
    { "target": "objects[0]", "property": "rotation.y", "from": 0, "to": 360, "duration": 3000, "loop": true }
  ]
}
```

**Implementation considerations:**
- Bundle a minimal HTML template with Three.js/Zdog in the app
- Panel type: `3d-scene:<path-to-scene.json>`
- Keep JSON schema simple for common use cases (rotating logos, ambient displays)
- Consider presets for common animations (spinning cube, orbiting spheres, etc.)
- Performance: limit polygon count and effects for smooth LCD refresh rates



## a panel that wraps the HTML panel to display a banner with settings for text, font, size and color

## an animated game plugin panel simulating various legacy games like pacman, tetris, ms pacman, asteroids, mario, zelda...

## functional tests

write a robust set of functional tests that starts by deleting the default profile yaml file, then uses the cli commands to create and modify the file - test each command under profile verb by actually calling the exe and then verify the actual file changes
