# ⛳ Foreplay: Par‑TEE Time

**Foreplay: Par‑TEE Time** is a whimsical, physics-driven Golf‑RPG built with **Godot 4** and **C#**. It combines the precision of a golf simulator with the creativity of a course builder and the progression of an RPG.

![Godot Engine](https://img.shields.io/badge/Godot-4.x-blue?logo=godot-engine&logoColor=white)
![C#](https://img.shields.io/badge/C%23-.NET%208.0-purple?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-brightgreen)

---

## 🌟 Vision
The goal of *Par-TEE Time* is to offer a golf experience that is as much about *building* the game as it is about *playing* it. Whether you're fine-tuning your swing physics or designing the ultimate 18-hole gauntlet, the game provides the tools to make the course your own.

---

## 🚀 Key Features

### 🏌️ Advanced Golf Physics
- **Dynamic Swing System**: A high-precision swing mechanic that factors in power, timing, and club selection.
- **Club Diversity**: Full set of clubs (woods, irons, wedges, putters) with unique trajectories and distances.
- **Environmental Factors**: Wind systems, terrain lies (fairway, rough, bunkers, green), and ball spin.

### 🛠️ Course Builder & Persistence
- **In-Game Editor**: Place trees, pins, markers, and hazards in real-time.
- **Persistence System**: Save and load custom course layouts effortlessly via the `CoursePersistenceManager`.
- **Custom Terrain**: Integrated heightmap-based terrain system for procedurally influenced landscapes.

### 📈 RPG Progression
- **Skill & Stat System**: Level up your golfer's abilities, including power, accuracy, and finesse.
- **Database Integration**: Player persistence powered by a robust SQLite backend.

### 🎨 Interactive Main Menu
- **Physics Playground**: Unlike static menus, our title screen features live physics where golf balls bounce off UI elements and pile up dynamically.
- **Responsive Layout**: UI colliders auto-generate and resize, ensuring the physics remain consistent across all resolutions.

---

## 🛠️ Technical Stack
- **Engine**: Godot 4 (Forward+ Renderer)
- **Language**: C# / .NET 8.0
- **Database**: SQLite (via `DatabaseManager`)
- **Data Serialization**: JSON & Godot Resources

---

## 🎮 Playtest coming soon!
We are currently in active development. Stay tuned for upcoming playtest opportunities!

---

## 📂 Project Structure
- **/Scenes**: Game levels, UI menus, and environment prefabs.
- **/Scripts**:
    - **/Systems**: Core logic (BuildManager, SwingSystem, WindSystem, Physics).
    - **/Entities**: Interactive objects, ball controllers, and player logic.
    - **/Data**: Persistence, course metadata, and skill definitions.
    - **/UI**: Controllers for the HUD, build menus, and main menu.
- **/Assets**: 3D models, textures, and sound effects.

---

## 🤝 Contributing
Contributions are welcome! Whether it's fixing bugs, adding new course assets, or refining the physics engine. Feel free to open issues or pull requests.

---

## 📜 Credits & License
Built with ❤️ by **Kael Kodes** and the **Antigravity AI Assistant**.  
*Foreplay: Par-Tee Time* is licensed under the MIT License.
