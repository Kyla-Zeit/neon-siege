# Neon Siege

**Neon Siege** is a small **C# Windows Forms arcade shooter** built in **VS Code**.  
This enhanced version turns the original prototype into a fuller little action game with menu flow, weapon upgrades, enemy variety, boss waves, enemy bullets, high-score saving, pause support, hit effects, and better progression.

## Features

- Real-time windowed arcade gameplay
- Keyboard movement and mouse aiming
- Left-click shooting with dash movement
- Multiple enemy types:
  - Chaser
  - Brute
  - Shooter
  - Splitter
  - Boss
- Enemy projectiles and boss spread attacks
- Score-based permanent weapon upgrades
- Random power-up drops:
  - Heal
  - Rapid Fire
  - Bomb
  - Spread Shot
  - Pierce Shot
  - Shield
- Pause screen and title screen
- Difficulty selection
- Local persistent high score save
- Screen shake, hit flash, particles, and health bars

## Controls

- **WASD / Arrow Keys** = Move
- **Mouse** = Aim
- **Hold Left Click** = Shoot
- **Space** = Dash
- **P / Esc** = Pause / Resume
- **Enter** = Start game
- **R** = Restart after death
- **Tab / Left / Right** = Change difficulty on title screen
- **H** = Toggle screen shake on title screen

## Tech Stack

- C#
- .NET 8
- Windows Forms
- VS Code

## Run Locally

### Requirements
- Windows
- .NET 8 SDK
- VS Code or Visual Studio

### Steps
```bash
git clone https://github.com/Kyla-Zeit/neon-siege.git
cd neon-siege
dotnet restore
dotnet run
```

## Portfolio Value

This project shows:

- Real-time game loops in C#
- Input handling for keyboard and mouse
- Collision detection
- Game-state management
- Enemy AI variants
- Score-based progression systems
- Local file persistence with JSON
- Rendering and effects using Windows Forms / GDI+

## Future Ideas

- Audio assets instead of system sounds
- Local leaderboard with top 10 runs
- More bosses and arenas
- Weapon selection menu
- Published executable build for easy sharing
