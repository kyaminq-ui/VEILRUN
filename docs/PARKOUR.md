# VEILRUN — Parkour Design & Systems

> Le déplacement est le cœur du projet. Crédibilité perceptive > simulation réaliste.
> Valeurs mesurées : `PARKOUR_METRICS.md`. Réglages : `Resources/Movement/Default{Movement,Traversal,Camera}Tuning.tres`.

## 1. Vocabulaire — état d'avancement (v0.2.0)

| Mouvement | Contrôle | Milestone | État |
|---|---|---|---|
| walk → run → sprint (progression automatique) | maintenir avancer | M1 | ✅ |
| momentum, virages coûteux, air control borné | — | M1 | ✅ |
| jump, coyote time, jump buffering | Espace | M1 | ✅ |
| réceptions Soft / Medium / Heavy / Deadly | — | M1 | ✅ |
| **crouch / crouch-walk** (discret, 2 m/s) | maintenir Ctrl / C | M2 | ✅ |
| **slide** + **slide jump** | accroupi à ≥ 5 m/s, puis Espace | M2 | ✅ |
| **step-up** (0.1–0.5 m) / **quick climb** (≤ 1.05 m) | avancer | M2 | ✅ |
| **vault** (obstacle fin 0.1–1.3 m) | avancer | M2 | ✅ |
| **mantle** (≤ 2.0 m du sol, ≤ 1.5 m en l'air) | avancer | M2 | ✅ |
| **ledge grab** + **ledge climb** + **ledge drop** + **shimmy** | automatique / avancer / accroupi / gauche-droite | M2 | ✅ |
| **wall run horizontal** + **wall jump** | sauter le long d'un mur en avançant / Espace | M2 | ✅ |
| **wall climb** (wall run vertical) + **wall kick** | Espace face à un mur haut / Espace pendant la montée (ou dos + Espace en suspension) | M2 | ✅ |
| **landing roll** | accroupi ≤ 0.35 s avant une réception Medium / Heavy | M2 | ✅ |
| land-into-slide | accroupi avant une réception Soft à vitesse | M2 | ✅ |
| corner transition, turn vault, drop-to-ledge, underbar, zipline, pipe, ladder, balance | — | post-M2 | ⏳ |

## 2. Principes de sensation
- **Le momentum se construit** : maintenir avancer passe de la marche (0.08 s) à la course (0.37 s) puis au sprint (1.33 s). Une bonne ligne est plus rapide ; un virage serré coûte de la vitesse.
- **Le momentum se garde** : réception Soft, vault (95 %), step-up (100 %), quick climb (85 %), slide (boost), roulade (90 %). Les mantles hauts et les réceptions dures coûtent cher : c'est la contrepartie de la verticalité.
- **Arcs prévisibles** : apex exact, gravité plus forte en descente, tolérances généreuses mais bornées.
- **Le corps est lisible** : hauteur des yeux selon la posture, penché à l'opposé du mur en wall run, plongée de caméra à la roulade, vue retournée au wall kick. Tous ces effets sont réglables et réduits par le mode confort.

## 3. Architecture (implémentée)

```
Runner (CharacterBody3D) ── tick 60 Hz ── PlayerInput → InputCommand
└── PlayerMotor (classe pure)                       Scripts/Player/PlayerMotor.cs
    ├── MotorState (struct, TOUT l'état, rollbackable)
    ├── Locomotion : sol / air / crouch / landing (+ roll / land-into-slide)
    ├── TraversalProbes   ← SEUL point d'accès à la physique pour la traversée
    │     box cast frontal + rest info, rayons (sommet, profondeur, bord opposé),
    │     rayons latéraux doublés (wall run), capsules de dégagement ; compteur de budget ; segments de debug
    └── TraversalContext  ← dispatch + cache de la sonde frontale par tick + chemins scriptés
          Moves/SlideMove.cs      Slide, Roll
          Moves/ObstacleMoves.cs  Vault, Mantle / quick climb / step-up, tick des chemins scriptés
          Moves/LedgeHangMove.cs  Hang, Climb, Drop, Shimmy, eject arrière
          Moves/WallMoves.cs      WallRun (+ wall jump), WallClimb (+ wall kick)
```

### Règles de conception
1. **Les moves sont sans état** (classes statiques). Tout ce qui doit survivre d'un tick à l'autre est dans `MotorState` (`Traversal`, `TraversalTime`, `TravStart/End/Normal/Dir`, `TravRiseTime/MoveTime/PeakY/Split/Speed`, `WallSide`, cooldowns, buffers, `IsCrouched`). Conséquence : rollback et replay exacts (testé).
2. **Deux familles de moves** :
   - *simulés* (Slide, Roll, WallRun, WallClimb) : vitesse calculée puis `PlayerMotor.MoveBody` → `MoveAndSlide` ;
   - *scriptés* (Vault, Mantle, LedgeClimb, snap de suspension) : position = `EvaluatePath(state, t)`, une trajectoire déterministe en deux phases (montée avec ease-out, puis translation). Le dégagement est **validé au départ** par des capsules de test ; aucune collision n'est calculée pendant le chemin (monde statique).
3. **Priorité de démarrage** (`TraversalContext.TryStartAny`) : au sol → wall climb (si un saut est bufferisé face à un mur haut) → slide → vault / mantle. En l'air → vault / mantle aérien / ledge grab → wall run.
4. **Premier tick immédiat** : un move qui démarre exécute son premier tick dans la même frame (aucun gel).
5. **Sortie des moves scriptés** : `SyncBodyContactState` recale le flag « au sol » interne de `CharacterBody3D` (sonde longue comme le floor snap, voir D-026) → pas de divergence en replay.
6. **Budget** : ≤ 8 queries par tick et par runner (mesuré : 7). La sonde frontale n'est lancée que si le joueur pousse vers l'avant ; les rayons latéraux seulement en l'air à ≥ 5 m/s.
7. **Surfaces** : métadonnée `surface` (audio) et `wallrun = false` (interdit le wall run) sur n'importe quel collider.

### Ajouter un nouveau move (checklist)
1. Ajouter une valeur à `TraversalKind` et, si besoin, des champs dans `MotorState` (pas d'état ailleurs !).
2. Ajouter les réglages dans `TraversalTuning` (avec unités et bornes).
3. Écrire `TryStart` / `Tick` dans `Scripts/Traversal/Moves/`, en passant **uniquement** par `TraversalProbes` pour la physique.
4. Le brancher dans `TraversalContext.TryStartAny` (priorité) et `Tick` (dispatch).
5. Définir la hauteur de capsule et des yeux (`PlayerMotor.CapsuleHeightFor` / `EyeHeightFor`) si la posture change.
6. Tests : comportement nominal + limites + ajout au parcours de déterminisme ; vérifier le budget de queries.
7. Présentation : son (`PlayerAudio.OnTraversalStarted`), caméra (`PlayerCamera`), HUD.
8. Mesurer, puis mettre à jour `PARKOUR_METRICS.md`.

## 4. Flow (plan M5)
`FlowLevel` ∈ [0,1] dérivé de : vitesse horizontale, chaîne de moves sans rupture (les événements `Started` / `Ended` existent déjà dans `MotorEvents`), qualité des réceptions (Soft ou roulée). Il servira au feedback (audio, caméra, musique) et au score ; ce n'est pas une barre arcade.

## 5. Réglage
Tout est éditable dans l'inspecteur Godot (`Default*Tuning.tres`). Après modification : `tools/run_tests.ps1`, puis reporter les lignes `METRIC` dans `PARKOUR_METRICS.md`.
