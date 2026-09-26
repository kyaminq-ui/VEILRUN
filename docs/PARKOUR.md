# VEILRUN — Parkour Design & Systems

> Le déplacement est le cœur du projet. Crédibilité perceptive > simulation réaliste.

## 1. Vocabulaire — état d'avancement

| Mouvement | Milestone | État |
|---|---|---|
| walk / run / sprint (progression automatique en maintenant avancer) | M1 | ✅ |
| accélération / décélération au sol, momentum | M1 | ✅ |
| jump, coyote time, jump buffering | M1 | ✅ |
| air control | M1 | ✅ |
| réceptions Soft / Medium / Heavy / Deadly | M1 | ✅ |
| caméra procédurale (bob, dip, roll, FOV) | M1 | ✅ |
| slide, slide jump | M2 | ⏳ |
| low / speed vault | M2 | ⏳ |
| mantle, ledge grab, ledge climb | M2 | ⏳ |
| wall run horizontal, wall jump | M2 | ⏳ |
| landing roll | M2 | ⏳ |
| wall run vertical, wall kick, shimmy, drop-to-ledge | M2+ | ⏳ |
| underbar, zipline, pipe, ladder, balance, turn vault | post-M2 | ⏳ |

## 2. Principes de sensation (M1)

- **Le momentum se construit** : maintenir avancer passe de la marche (0.08 s) à la course (0.37 s) puis au sprint (1.33 s). Pas de touche sprint ; une bonne ligne est plus rapide, un virage serré coûte de la vitesse.
- **Le momentum se garde** : une réception Soft conserve 100 % de la vitesse ; en l'air on oriente la trajectoire sans gagner de vitesse (plafond de 2.5 m/s hors vitesse d'entrée).
- **Arcs prévisibles** : apex exact (1.20 m), chute plus lourde que la montée (×1.4), tolérances généreuses mais bornées (coyote et buffer de 0.12 s).
- **Impact lisible** : dip caméra proportionnel à la vitesse d'impact ; réceptions Medium / Heavy = vraies pénalités, Deadly = mort.

## 3. Architecture de la traversée (plan M2)

```
Runner
├── PlayerMotor              (locomotion de base : sol / air)
└── TraversalSystem          (Veilrun.Traversal)
    ├── TraversalProbeSystem   ← UN SEUL passage de probes par tick, résultats mis en cache
    │     shape casts centralisés : obstacle frontal, sommet du rebord, dégagement, mur latéral, sol d'atterrissage
    ├── TraversalCandidate     ← opportunité détectée + score (angle, vitesse, input, hauteur)
    ├── TraversalStateMachine  ← états exclusifs : Locomotion | Slide | Vault | Mantle | LedgeHang | WallRun | Roll
    ├── TraversalState         ← Enter / Tick(cmd) / Exit, déterministes, sans nœuds
    ├── TraversalContext       ← accès motor + probes + tuning
    ├── TraversalTuning        ← Resource (plages vault/mantle, durée wall-run, fenêtres…)
    └── TraversalSurface       ← métadonnées de surface (autorise wall-run ? vault ? SurfaceType audio)
```

Règles :
- Aucune mécanique critique ne repose sur un seul raycast : combinaison de shape casts + contrôle de dégagement de capsule.
- Les états de traversée pilotent la position par des **courbes déterministes** (pas de root motion autoritaire).
- Chaque probe a sa visualisation dans `DebugDraw` (obligatoire).
- `MotorState` s'étendra avec l'état de traversée (enum + timer + données de l'ancre) pour rester rollbackable.

## 4. Flow (plan)
`FlowLevel` ∈ [0,1] dérivé de : vitesse horizontale, chaîne de traversées sans rupture, qualité des réceptions. Il sert au feedback (audio, caméra, musique) et au score ; ce n'est **pas** une barre arcade. `TraversalChain` et `LandingQuality` seront ajoutés à `MotorEvents` en M2.

## 5. Réglage
Tous les paramètres sont dans `Resources/Movement/DefaultMovementTuning.tres` et `DefaultCameraTuning.tres` (inspecteur Godot). Après modification : `tools/run_tests.ps1`, puis mettre à jour `PARKOUR_METRICS.md` avec les lignes `METRIC`.
