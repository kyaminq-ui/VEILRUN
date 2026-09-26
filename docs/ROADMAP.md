# VEILRUN — Roadmap

Priorités : FUN → RELIABILITY → NETWORKING → READABILITY → CONTENT → POLISH.

| Milestone | Contenu | État |
|---|---|---|
| **M0 — Foundation** | projet C# propre, Git, input, debug framework, docs, map de test | ✅ terminé (v0.1.0) |
| **M1 — Player Motor** | walk / run / sprint, jump, gravité, air control, landing, caméra | ✅ validé par la direction ; feedback intégré en v0.1.1 (momentum automatique, 4 paliers de réception, audio) |
| **M2 — Core Parkour** | slide, vault, mantle, ledge, wall run, wall jump, roll + gym | ✅ **implémenté (v0.2.0)** : 35 tests verts, vérifié en jeu via MCP. Reste le playtest de ressenti humain |
| M3 — Networked Parkour | serveur dédié, join, réplication, prediction, reconciliation, interpolation | ⏭️ **prochaine milestone** |
| M4 — Contract Hunt prototype | lobby, match state, contrats, identification, interception, score, respawn, résultats | ⏳ |
| M5 — Chase & Stealth | menace, chase, escape, contre, signal, leurres / NPC | ⏳ |
| M6 — First competitive map | greybox 3+ couches, routes multiples, playtests | ⏳ |
| M7 — Character & Animation | corps FP, runner TP, AnimationTree, IK, anim réseau | ⏳ |
| M8 — Art / Audio / VFX | kit d'environnement, lumière, SFX, musique, UI — 60 FPS | ⏳ |
| M9 — Alpha | features verrouillées, bugs, équilibrage, perf | ⏳ |
| M10 — Beta / Release prep | settings, accessibilité, déploiement serveur, crash handling, légal | ⏳ |

## Prochaines tranches (ordre proposé)

1. **M2-close — Playtest humain** (≈ 1 session) : parcourir la gym (F6 pour les lanes du Parkour Lab) au clavier et à la manette ; ajuster `DefaultTraversalTuning.tres` et `DefaultMovementTuning.tres` ; reporter les nouvelles valeurs `METRIC` dans `PARKOUR_METRICS.md`.
2. **M3-a — Réseau minimal** : `IInputSource` (local / réseau / replay), serveur headless (`--server`), `ENetMultiplayerPeer`, join de 2 clients, réplication des positions *sans* prediction (baseline mesurée).
3. **M3-b — Prediction + reconciliation** : ring buffer `(InputCommand, MotorState)`, correction par `RestoreState` + replay (le contrat est déjà testé), lissage visuel des corrections.
4. **M3-c — Interpolation des joueurs distants** + représentation 3ᵉ personne placeholder (capsule orientée) + audio positionnel des pas.
5. **M3-d — Conditions dégradées** : simulateur de latence / perte / jitter (30 / 80 / 150 / 250 ms, 1–5 %), Dev HUD réseau (ping, perte, corrections, erreur de prediction), sérialisation compacte de `MotorState`.
6. **M3-e — Validation serveur** : clamp des inputs, contrôle de vitesse, rejet des séquences invalides, journal des anomalies.

> Le risque principal de M3 est la reconciliation pendant les moves scriptés et le wall run. Le contrat de replay est déjà vérifié sur un parcours complet. Il faudra le re-vérifier entre binaire client Windows et serveur Linux (écarts flottants possibles).
