# VEILRUN — Technical Design Document

Version 0.1.0 — 2026-09-26 — état : fin M0 / début M1.

## 1. Stack

| Domaine | Choix |
|---|---|
| Moteur | Godot 4.7.2-stable **Mono**, Forward+, D3D12 (Windows) |
| Langage runtime | C# 12, .NET 8 (`Godot.NET.Sdk/4.7.2`), Nullable activé |
| Physique | Jolt Physics (intégrée à Godot 4.7) |
| Tick simulation | 60 Hz (`physics_ticks_per_second` par défaut) |
| Tests | runner in-engine headless (`Scenes/Tests/TestRunner.tscn`) |
| Outils éditeur | MCP Godot-AI (plugin `addons/godot_ai`, GDScript, éditeur uniquement) |

## 2. Arborescence

```
res://
├── Assets/            # assets de production (vides en M0)
├── Dev/
│   ├── TestMaps/MovementGym.tscn   (généré — tools/generators)
│   └── Textures/Grid/              textures prototypes
├── Resources/
│   ├── Movement/   DefaultMovementTuning.tres, DefaultCameraTuning.tres
│   └── Settings/   DefaultUserSettings.tres
├── Scenes/
│   ├── Characters/Runner.tscn
│   └── Tests/TestRunner.tscn
└── Scripts/
    ├── Core/           Boot, InputActions, InputDefaults, Log, MathUtil, Settings/UserSettings
    ├── Player/         Runner, PlayerInput, PlayerMotor, PlayerCamera, LocomotionMath,
    │                   MotorState, InputCommand, MovementTuning, CameraTuning
    ├── Tools/Debug/    DevTools (autoload), DevHud, DebugDraw, IDebugInfoProvider
    └── Tests/          Framework/ (runner, harness), LocomotionMathTests, PlayerMotorTests
/docs  /tools  /build
```

Namespaces : `Veilrun.Core`, `Veilrun.Core.Settings`, `Veilrun.Player`, `Veilrun.Tools.Debug`, `Veilrun.Tests` (+ à venir `Veilrun.Traversal`, `Veilrun.Networking`, `Veilrun.GameModes`, `Veilrun.UI`, `Veilrun.Audio`, `Veilrun.World`).

## 3. Autoloads (volontairement limités)

| Ordre | Nom | Rôle |
|---|---|---|
| 1 | `_mcp_game_helper` | plugin Godot-AI (debug uniquement — à exclure en release) |
| 2 | `Boot` | setup process-wide : bindings par défaut, log de version |
| 3 | `DevTools` | Dev HUD + DebugDraw + registre `IDebugInfoProvider` |

Pas de service locator. `DevTools.Find(node)` retourne `null` si absent : les appelants doivent le tolérer.

## 4. Personnage

```
Runner (CharacterBody3D, Runner.cs)          ← racine de composition, boucle de tick
├── Collision (CollisionShape3D)             ← dimensions imposées par MovementTuning
├── BodyPlaceholder (MeshInstance3D)         ← ombre uniquement (cast_shadow = shadows only)
├── PlayerInput (Node)                       ← périphériques → InputCommand
└── PlayerCamera (Node3D, top_level)         ← caméra procédurale
    └── Camera3D
PlayerMotor  (classe C#, pas un Node)        ← simulation
```

### 4.1 Boucle de tick (`Runner._PhysicsProcess`, 60 Hz)
1. `PlayerInput.Sample(tick)` → `InputCommand { Sequence, Tick, Move, Yaw, Pitch, Buttons }`
2. `PlayerMotor.Simulate(cmd, dt)` → `MotorEvents { Jumped, LeftGround, Landed? }`
3. Publication des événements (`Jumped`, `Landed`, `Respawned`) → caméra, et plus tard audio / anim / télémétrie.

### 4.2 Contrat du motor
- Entrées : `MotorState` + `InputCommand` + `MovementTuning` + monde statique.
- Sortie déterministe ; `CaptureState` / `RestoreState` permettent rollback + replay (testé à erreur 0.000 m).
- Les boutons sont « maintenu OU pressé depuis le dernier échantillon » (aucun tap perdu) ; les fronts sont dérivés dans le motor via `MotorState.PreviousButtons`.
- `Pitch` n'influence jamais le mouvement au sol.

### 4.3 Modèle de locomotion (`LocomotionMath`)
- **Sol** : cap et vitesse intégrés séparément. Taux de rotation qui dépend de la vitesse (20 rad/s en course → 7 rad/s en sprint). Accélération en deux étages (35 m/s² jusqu'à la vitesse de course, 3 m/s² au-delà). Les virages font perdre de la vitesse au-delà de la vitesse de course. Un input vers l'arrière freine.
- **Air** : accélération 10 m/s², plafonnée à `max(vitesse d'entrée, 5.5 m/s)` → on peut orienter la trajectoire sans gagner de vitesse.
- **Vertical** : gravité asymétrique (montée / descente ×1.4), intégration en demi-pas (D-005), vitesse terminale 45 m/s.
- **Réception** : classée Soft / Hard / Fatal selon la vitesse d'impact dérivée des hauteurs de chute.

## 5. Caméra
Voir D-007. Effets (tous réglables dans `CameraTuning`, modulés par `UserSettings`) : head bob indexé sur la distance parcourue, ressort d'atterrissage (dip + pitch kick), roll en strafe, FOV dynamique. Le mode *Reduced Camera Motion* limite tous les effets à 25 % et coupe le FOV dynamique et le roll.

## 6. Outils de debug
- **Dev HUD (F1)** : FPS, temps de frame et de physique, tick, mode, position / vitesse, h-speed, v-speed, coyote, jump buffer, recovery, dernière réception ; emplacements réservés pour flow / traversal / target / pursuer / réseau.
- **Debug Draw (F2)** : lignes sans depth test ; capsule (vert = au sol, orange = en l'air), vecteur vitesse, normale du sol. API : `Line`, `Arrow`, `Cross`, `Capsule` (durée de vie optionnelle pour les appels à fréquence physique).
- **F4** : respawn. **Échap** : libère la souris (un clic la recapture).

## 7. Tests
`tools/run_tests.ps1 [-Filter X]` : build puis Godot `--headless` sur `TestRunner.tscn`. 22 tests (9 mathématiques pures + 13 physiques Jolt). Les tests physiques exécutent N ticks dans une seule frame physique : même mécanique que le replay de reconciliation.

## 8. Contrôles par défaut (keycodes physiques)
| Action | Clavier (position QWERTY) | AZERTY | Manette |
|---|---|---|---|
| Déplacement (maintenir avancer = marche → course → sprint) | W A S D | Z Q S D | stick gauche (< 60 % = marche) |
| Regard | souris | souris | stick droit |
| Saut | Espace | Espace | A / Croix |
| Accroupi (réservé M2) | Ctrl / C | Ctrl / C | B / Rond |
