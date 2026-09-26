# VEILRUN

FPS parkour multijoueur compétitif, chasse et contre-chasse. Godot 4.7.2 Mono · C#.

## Démarrer
1. Ouvrir le projet avec **Godot 4.7.2 Mono** puis lancer (F5) : la scène principale est la **Movement Gym**.
2. Contrôles : ZQSD / WASD (positions physiques ; maintenir avancer = marche → course → sprint) · souris · Espace saut · **F1** Dev HUD · **F2** debug draw · **F4** respawn · Échap libère la souris.

## Build & tests
```powershell
dotnet build VEILRUN.csproj
./tools/run_tests.ps1                 # suite complète (headless), code de sortie 0/1
./tools/run_tests.ps1 -Filter Motor   # filtre par nom
```
Chemin de Godot : variable `GODOT`, sinon `~/Desktop/Godot_v4.7.2-stable_mono_win64/…_console.exe`.

## Documentation
Tout est dans [`docs/`](docs/) : commencer par `ROADMAP.md`, `TDD.md` et `PARKOUR_METRICS.md`. Décisions d'architecture : `DECISIONS.md`.

## Régénérer la gym
```
python tools/generators/gen_movement_gym.py
```
