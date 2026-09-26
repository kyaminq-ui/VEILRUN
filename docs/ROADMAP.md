# VEILRUN — Roadmap

Priorités : FUN → RELIABILITY → NETWORKING → READABILITY → CONTENT → POLISH.

| Milestone | Contenu | État |
|---|---|---|
| **M0 — Foundation** | projet C# propre, Git, input, debug framework, docs, map de test | ✅ **terminé** (2026-09-26) |
| **M1 — Player Motor** | walk / run / sprint, jump, gravité, air control, landing, caméra | 🟡 **implémenté et testé automatiquement — reste la validation du ressenti en playtest** |
| M2 — Core Parkour | slide, vault, mantle, ledge, wall run, wall jump, roll + gym | ⏳ |
| M3 — Networked Parkour | serveur dédié, join, réplication, prediction, reconciliation, interpolation | ⏳ |
| M4 — Contract Hunt prototype | lobby, match state, contrats, identification, interception, score, respawn, résultats | ⏳ |
| M5 — Chase & Stealth | menace, chase, escape, contre, signal, leurres / NPC | ⏳ |
| M6 — First competitive map | greybox 3+ couches, routes multiples, playtests | ⏳ |
| M7 — Character & Animation | corps FP, runner TP, AnimationTree, IK, anim réseau | ⏳ |
| M8 — Art / Audio / VFX | kit d'environnement, lumière, SFX, musique, UI — 60 FPS | ⏳ |
| M9 — Alpha | features verrouillées, bugs, équilibrage, perf | ⏳ |
| M10 — Beta / Release prep | settings, accessibilité, déploiement serveur, crash handling, légal | ⏳ |

## Prochaines tranches (ordre proposé)

1. **M1-close — Playtest de ressenti** (humain) : parcourir la gym au clavier / souris et à la manette ; ajuster `DefaultMovementTuning.tres` ; mettre à jour les métriques. Ajouter la persistance de `UserSettings` et un footstep/landing audio placeholder (le son fait partie du ressenti).
2. **M2-a — Traversal framework + Slide** : `TraversalProbeSystem` centralisé avec visualisation debug, `TraversalStateMachine` rollbackable, slide et slide-jump, et leurs tests.
3. **M2-b — Vault + Mantle** (validés sur les blocs de hauteur de la gym).
4. **M2-c — Ledge grab / climb / drop**.
5. **M2-d — Wall run + wall jump**, puis **landing roll**.
6. **M3-spike (en parallèle, tôt)** : 2 clients + 1 serveur headless, déplacement M1 seul, latence simulée. Objectif : valider la reconciliation avant d'empiler le parkour.

> Recommandation : faire le spike M3 juste après M2-a, pas après tout M2. Les mouvements de traversée sont le principal risque pour la reconciliation, et il vaut mieux le découvrir avec un seul état qu'avec sept.
