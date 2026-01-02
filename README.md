# Foreplay – Par‑TEE Time

A whimsical golf‑RPG built with Godot 4 and C#.  

## What's New (v0.9‑ish)
- **Main‑Menu physics polish** – 3D golf balls now fall, bounce off UI elements, and pile up at the bottom of the screen.
- **Dynamic UI colliders** – Invisible `StaticBody3D` boxes are generated from every button and label, keeping the balls interacting with the menu layout even after window resizes.
- **Lighting overhaul** – A `WorldEnvironment` with ambient light and a front‑facing `DirectionalLight3D` gives the balls a clean, bright look without the previous “nuclear glow”.
- **Performance guard** – A 50‑ball cap prevents lag while still looking lively.

## How to Play
Run the project in Godot.  
The main menu now has a lively background; click **Driving Range** or **Putting Range** to start a round.

## Contributing
Feel free to open issues or PRs.  
The repository follows the standard Godot project layout and uses a `.gitignore` that excludes the `.import` and `mono` build folders.

---
*Built with love by Kael Kodes and the Antigravity AI assistant.*
