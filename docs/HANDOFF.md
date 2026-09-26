# VEILRUN — Handoff (reprise à froid)

**Dernière mise à jour : 2026-09-26 — version 0.2.0 — fin de M2 (Core Parkour).**
Dernier commit poussé : `VEILRUN 0.2.0 — M2 Core Parkour` sur `origin/main` (https://github.com/kyaminq-ui/VEILRUN).
À mettre à jour à la fin de chaque milestone, en même temps que `resume_prompt.md`.

---

## 1. En une minute
- **Jouable** : un runner en première personne dans une *Movement Gym* générée : locomotion à momentum (walk → run → sprint en maintenant avancer), saut, 4 paliers de réception et **tout le parkour M2** (slide, vault, mantle, step-up, ledge grab / climb / drop / shimmy, wall run, wall jump, wall climb, wall kick, landing roll, crouch-walk). Caméra procédurale, audio placeholder, Dev HUD.
- **Solide** : 35 tests automatisés dans le moteur (dont 2 tests de déterminisme par rollback / replay), 0 avertissement de compilation, vérifié en jeu via le MCP Godot-AI.
- **Pas encore** : réseau (M3), mode de jeu (M4+), vrais assets (M7-M8).

## 2. Environnement
| Élément | Valeur |
|---|---|
| OS de dev | Windows 11 Pro, shell Git Bash + PowerShell 5.1 |
| Projet | `C:\Users\Admin\Documents\veilrun` |
| Godot | 4.7.2-stable mono : `~/Desktop/Godot_v4.7.2-stable_mono_win64/` (`…_console.exe` pour le headless) |
| .NET | SDK 9.0.318, cible `net8.0`, `Godot.NET.Sdk/4.7.2` |
| MCP | `godot-ai` (plugin `addons/godot_ai` v4.2.3, éditeur ouvert requis), `blender`, `claude-in-chrome` |
| Git | `main` suivie sur `origin` ; LFS actif (WAV, SWF, etc.) ; compte GitHub `kyaminq-ui` authentifié via `gh` |
| Locale | française (virgules décimales dans les sorties) |

## 3. Carte du code (5 100 lignes C#)
```
Scripts/
├── Core/          Boot (autoload), InputActions, InputDefaults (bindings physiques), Log, MathUtil, Settings/UserSettings
├── Player/
│   ├── Runner.cs            racine de composition (CharacterBody3D) : tick 60 Hz, événements, respawn, F4 / F6, HUD, debug draw
│   ├── PlayerMotor.cs       simulation : locomotion, landing / roll, capsule, services pour la traversée, rollback
│   ├── MotorState.cs        état complet (struct) + LocomotionMode, LandingType, LandingEvent, MotorEvents
│   ├── LocomotionMath.cs    maths pures (sol, air, gravité, paliers, TopSpeedFor)
│   ├── MoveInput.cs / InputCommand.cs / PlayerInput.cs   intention joueur (commande réseau-ready) + turn-around
│   ├── PlayerCamera.cs / CameraTuning.cs                  caméra procédurale
│   ├── PlayerAudio.cs                                     audio de mouvement (présentation)
│   └── MovementTuning.cs                                  tuning locomotion
├── Traversal/     TraversalContext (dispatch), TraversalProbes (physique), TraversalTuning, TraversalKind
│   └── Moves/     SlideMove(+Roll), ObstacleMoves (vault / mantle / chemins scriptés), LedgeHangMove, WallMoves (wall run / climb)
├── Audio/         SurfaceType (+ métadonnée `surface`), SfxEvent, MovementAudioBank
├── Tools/Debug/   DevTools (autoload), DevHud, DebugDraw, IDebugInfoProvider
└── Tests/         Framework/ (TestRunner, TestFramework, MotorHarness), LocomotionMathTests (9), PlayerMotorTests (13), TraversalTests (13)
Scenes/Characters/Runner.tscn · Scenes/Tests/TestRunner.tscn · Dev/TestMaps/MovementGym.tscn (générée)
Resources/Movement/Default{Movement,Traversal,Camera}Tuning.tres · Resources/Audio/DefaultMovementAudio.tres · Resources/Settings/DefaultUserSettings.tres
tools/ run_tests.{ps1,sh} · generators/{gen_movement_gym, gen_placeholder_sfx, gen_movement_audio_bank}.py
```

### Flux d'un tick
`Runner._PhysicsProcess` → `PlayerInput.Sample(tick)` → `InputCommand` → `PlayerMotor.Simulate` :
buffers et cooldowns → si un move est actif : `TraversalContext.Tick` ; sinon `TryStart` (un move démarre et joue son 1ᵉʳ tick) ; sinon `Locomotion` → `MotorEvents` → `Runner` publie (`Jumped`, `Landed`, `Footstep`, `TraversalStarted/Ended`, `TurnAround`) → caméra et audio réagissent.

## 4. Ce qui est vérifié
- `dotnet build` : 0 avertissement, 0 erreur.
- `tools/run_tests.sh` : **35 / 35**. Métriques clés dans `PARKOUR_METRICS.md` (apex 1.200 m, sprint 8 m/s en 1.33 s, saut en sprint 5.33 m, slide jump 5.74 m, wall run 12.3 m, wall climb jusqu'à 3.75 m, probes 7/8, erreur de replay 0.000 m).
- En jeu (MCP) : progression walk → sprint, Deadly → respawn, surface métal, wall climb + ledge sur 3.5 m, wall run avec penché de caméra, F6, aucune erreur dans les logs.
- **Pas vérifié** : ressenti humain de M2 au clavier et à la manette ; manette en général ; AZERTY physique réel (seulement des événements injectés).

## 5. Décisions en attente de la direction
| Sujet | Contexte | Où |
|---|---|---|
| Marche debout discrète au clavier | le crouch-walk existe ; faut-il une touche « composure » pour se fondre dans la foule en M4 ? | KI-09, D-015, D-023 |
| Réglages de ressenti M2 | valeurs par défaut raisonnables, non playtestées par un humain | `DefaultTraversalTuning.tres` |
| Direction artistique | palette provisoire | `ART_BIBLE.md` |
| Licence des textures de grille | probablement Kenney CC0, à confirmer | `ASSET_PROVENANCE.md` |
| `SFX_PACK/` local | à supprimer du disque (audio Mirror's Edge, inutilisable) | D-017 |

## 6. Prochaine milestone : M3 — Networked Parkour
Plan détaillé : `docs/ROADMAP.md` (tranches M3-a à M3-e) et `docs/NETWORKING.md`. Première tranche recommandée :
1. **`IInputSource`** (local / réseau / replay) pour découpler `Runner` de `PlayerInput` (TD-01).
2. **Boot réseau** : `--server` (headless) ou `--connect <ip>` ; `ENetMultiplayerPeer` ; spawn d'un `Runner` par peer. **Vérifier chaque API réseau Godot 4.7.2 via le MCP avant de coder.**
3. Serveur autoritaire : applique les `InputCommand` reçues (buffer par tick), diffuse les snapshots de `MotorState` (compactés) à 20–30 Hz.
4. Client : prediction (ring buffer `(InputCommand, MotorState)`), reconciliation par `RestoreState` + replay (le contrat est déjà testé), lissage visuel.
5. Distants : interpolation à ~100 ms + capsule orientée + pas positionnels.
6. Outils : simulateur de latence / perte, HUD réseau (ping, perte, corrections, erreur de prediction), test à 2 clients + 1 serveur en local (`tools/run_local_match.ps1` à créer).

Définition de done de M3 : le parkour reste jouable à 150 ms et 2 % de perte, avec 2 clients séparés.

## 7. Où sont les choses
| Besoin | Fichier |
|---|---|
| Règles du projet (complètes) | `docs/MASTER_PROMPT.md` |
| Règles opérationnelles et pièges | `CLAUDE.md` |
| Décisions (D-001 → D-027) | `docs/DECISIONS.md` |
| Problèmes connus et dette | `docs/KNOWN_ISSUES.md` (KI-01 → KI-17, TD-01 → TD-07) |
| Métriques de level design | `docs/PARKOUR_METRICS.md` |
| Architecture du parkour + ajout d'un move | `docs/PARKOUR.md` |
| Architecture globale | `docs/TDD.md` |
| Historique | `CHANGELOG.md`, `git log` |
