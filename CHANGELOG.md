# Changelog

Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/). Versions : `0.<milestone>.<patch>` jusqu'à l'alpha.

## [0.2.0] — 2026-09-26 — M2 Core Parkour

### Added
- **Traversée** (`Scripts/Traversal`) : slide et slide jump, crouch / crouch-walk, step-up, quick climb, vault, mantle, ledge grab / climb / drop / shimmy / eject arrière, wall run et wall jump, wall climb et wall kick (vue retournée), landing roll, land-into-slide.
- `TraversalProbes` : sondes centralisées (box cast + rayons + capsules de dégagement), budget de queries (mesuré 7/8), segments de debug (F2).
- `TraversalTuning` (`Resources/Movement/DefaultTraversalTuning.tres`), `MoveInput`, nouveaux champs rollbackables dans `MotorState`, événements `Started` / `Ended` / `WallJumped` / `TurnAround`, `LandingEvent.Rolled`.
- Caméra : hauteur des yeux selon la posture, penché en wall run, plongée à la roulade. Audio : hand plant, slide loop, roulade (placeholders procéduraux).
- Movement Gym : **Parkour Lab** (rambardes, blocs, murs de rebord / wall climb, barre de slide, tunnel accroupi, couloir de wall run) et 12 points de spawn de dev (**F6**).
- 13 tests de traversée, dont un parcours de déterminisme (vault → slide → mantle → wall run) rejoué depuis 180 checkpoints.

### Fixed
- La resynchronisation du flag « au sol » après un move scripté pouvait rater le sol (divergence de replay de 15 cm) — D-026.
- Les bordures de 0.15–0.35 m bloquaient un runner (D-025).

## [0.1.1] — 2026-09-26 — M1 feedback pass

### Changed
- **Locomotion** : suppression des touches marche (Alt) et sprint (Shift). Maintenir avancer fait passer marche → course → sprint (paliers 30 / 12 / 2.2 m/s²), vitesse max selon la direction (strafe 4.5, arrière 3.0), perte de momentum en virage renforcée (0.3/rad), air control plafonné à 2.5 m/s.
- **Réceptions** : 4 paliers Soft < 3.20 m ≤ Medium < 5.12 m ≤ Heavy < 6.45 m ≤ Deadly, avec des pénalités par palier.
- Head bob synchronisé sur la foulée de la simulation.
- Movement Gym : repères de chute aux seuils de réception, rampes en surface métal.

### Added
- Audio de mouvement : `PlayerAudio`, `MovementAudioBank`, `SfxEvent`, `SurfaceType` (métadonnée `surface`), bus audio, 31 sons placeholder générés procéduralement.
- Événement `Footstep` émis par le motor ; tests : progression walk / run / sprint, strafe / arrière, momentum sur saut et virage, paliers de réception, cadence des pas (22 tests).

### Removed
- `SFX_PACK/` exclu (audio extrait de Mirror's Edge, voir D-017).

## [0.1.0] — 2026-09-26 — M0 Foundation + M1 Player Motor (first pass)

### Added
- Projet C# `VEILRUN.csproj` / `VEILRUN.sln` (.NET 8, Godot.NET.Sdk 4.7.2), arborescence cible, dépôt Git + Git LFS, `.editorconfig`, configuration VS Code (build, tests, debug).
- Autoloads `Boot` (bindings par défaut en keycodes physiques, compatibles AZERTY) et `DevTools` (Dev HUD F1, Debug Draw F2).
- Personnage `Scenes/Characters/Runner.tscn` : `Runner` (racine de composition, tick 60 Hz), `PlayerInput`, `PlayerMotor`, `PlayerCamera`.
- Locomotion : walk / run / sprint avec momentum progressif, turn rate dépendant de la vitesse, perte de vitesse en virage, air control borné, saut (apex exact), coyote time, jump buffer, réceptions soft / hard / fatal, respawn (F4, kill plane).
- Caméra procédurale : interpolation entre ticks, head bob, dip d'atterrissage à ressort, roll en strafe, FOV dynamique ; options de confort (Reduced Camera Motion, Head Bob, Camera Roll, Landing Shake, Dynamic FOV, FOV).
- Resources de tuning : `MovementTuning`, `CameraTuning`, `UserSettings`.
- Contrat de simulation prêt pour le réseau : `InputCommand`, `MotorState`, `CaptureState` / `RestoreState` avec replay déterministe.
- Movement Gym générée (`Dev/TestMaps/MovementGym.tscn`) : gaps, hauteurs, hauteurs de chute, pentes, mur, couloir, porte.
- Runner de tests in-engine headless + 18 tests (math, physique, métriques, déterminisme du replay) ; `tools/run_tests.ps1` / `.sh`.
- Documentation : GDD, TDD, ROADMAP, DECISIONS, NETWORKING, PARKOUR, PARKOUR_METRICS, MULTIPLAYER_MODE, ART_BIBLE, AUDIO_BIBLE, ASSET_PROVENANCE, PERFORMANCE, KNOWN_ISSUES, AUDIT.

### Changed
- Textures de grille déplacées de `res://grid_textures/` vers `res://Dev/Textures/Grid/`.
