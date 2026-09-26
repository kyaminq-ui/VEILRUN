# VEILRUN — Architecture Decision Records

Format : Problem · Decision · Reason · Alternatives considered · Consequences · Date.
Une décision n'est jamais supprimée : on la marque **Superseded by D-xxx**.

---

## D-001 — C# exclusif pour le runtime
- **Problem** : Godot supporte GDScript et C#. Mélanger les deux fragmente l'architecture et le tooling.
- **Decision** : Tout le code runtime est en C# (.NET 8, `Godot.NET.Sdk/4.7.2`, `Nullable` activé). GDScript toléré uniquement dans des plugins éditeur tiers (`addons/godot_ai`).
- **Reason** : typage fort, refactoring, tests, performance CPU pour la simulation et le serveur.
- **Alternatives** : GDScript (prototypage plus rapide, mais typage faible et pas de tests hors moteur) ; mixte (dette de cohérence).
- **Consequences** : l'autoload `_mcp_game_helper` (GDScript, plugin Godot-AI) est présent en debug ; il devra être exclu des exports release (voir KNOWN_ISSUES).
- **Date** : 2026-09-26

## D-002 — Motor kinematic à tick fixe basé sur CharacterBody3D
- **Problem** : il faut un déplacement précis, prévisible, et compatible prediction / reconciliation (M3).
- **Decision** : `PlayerMotor` (classe C# pure) simule **un tick = une `InputCommand`** à 60 Hz. Il utilise `CharacterBody3D.MoveAndSlide()` uniquement pour la collision. Tout l'état de gameplay est dans une struct copiable `MotorState` (`CaptureState` / `RestoreState`).
- **Reason** : `MoveAndSlide` est robuste (glissements, pentes, snap) et interroge directement l'espace physique, donc on peut rejouer N ticks dans une seule frame physique (replay de reconciliation). RigidBody est exclu (non déterministe, non contrôlable).
- **Alternatives** : motor 100 % maison à base de `ShapeCast` (plus de contrôle, beaucoup plus de code et de bugs de collision) ; RigidBody (rejeté par le brief).
- **Consequences** : `Simulate()` doit être appelé pendant une frame physique (`MoveAndSlide` intègre sur le pas physique). Les plateformes mobiles ne font pas partie du contrat de déterminisme pour l'instant.
- **Date** : 2026-09-26

## D-003 — `Runner` comme racine de composition ; motor hors du scene tree
- **Problem** : éviter un `PlayerController` monolithique ; une classe `Player` dans le namespace `Veilrun.Player` provoque des ambiguïtés C# (CS0118) depuis les autres namespaces.
- **Decision** : le nœud racine du personnage est `Runner : CharacterBody3D`. Il orchestre le tick (input → motor → événements). Les composants sont : `PlayerInput` (Node), `PlayerMotor` (classe pure), `PlayerCamera` (Node3D). Les futurs composants (Traversal, Networking, Animation, Audio, Combat…) se branchent sur `Runner` au lieu de le grossir.
- **Reason** : politique « un Node seulement s'il a besoin du scene tree » ; testabilité du motor sans caméra ni input.
- **Consequences** : la présentation (caméra, anim, audio) lit l'état et les événements, n'écrit jamais l'état de simulation.
- **Date** : 2026-09-26

## D-004 — Tuning exprimé en intentions de design
- **Problem** : régler gravité et vitesse de saut à la main est contre-intuitif pour les designers.
- **Decision** : `MovementTuning` expose `JumpHeight` + `JumpTimeToApex`, et des **hauteurs** de chute (`HardLandingHeight`, `FatalFallHeight`). Gravité, vitesse de saut et vitesses d'impact sont **dérivées**.
- **Reason** : les métriques de level design (PARKOUR_METRICS) se lisent directement dans le tuning.
- **Date** : 2026-09-26

## D-005 — Intégration de la gravité en demi-pas
- **Problem** : Euler semi-implicite sous-estime l'apex (~5 cm à 60 Hz) et fait dériver les métriques avec le tick rate.
- **Decision** : la gravité est appliquée en deux demi-pas, avant et après `MoveAndSlide`.
- **Reason** : trajectoire balistique exacte à gravité constante ; apex mesuré = 1.200 m pour 1.2 m demandés.
- **Date** : 2026-09-26

## D-006 — Git LFS pour les assets binaires de production
- **Decision** : `.gitattributes` route vers LFS : blend, fbx, glb, obj, psd, exr, hdr, wav, ogg, mp3, flac, mp4, swf, ttf, otf. PNG/JPG **pas encore** (textures dev légères) — à réévaluer en M8.
- **Reason** : migrer vers LFS après coup impose de réécrire l'historique.
- **Consequences** : l'hébergeur Git distant doit supporter LFS (GitHub, GitLab, Azure DevOps : oui).
- **Date** : 2026-09-26

## D-007 — Caméra procédurale top-level, interpolation manuelle
- **Problem** : simulation à 60 Hz, affichage à 144 Hz+ : une caméra attachée au corps saccade ; la physics interpolation globale de Godot complique le contrôle fin et le futur netcode.
- **Decision** : `PlayerCamera` est `top_level`. Chaque frame, elle interpole la position des pieds entre les deux derniers ticks (`Engine.GetPhysicsInterpolationFraction()`), lit le look **à la fréquence d'affichage** (réactivité souris), puis ajoute bob / dip d'atterrissage / roll / FOV dynamique. La physics interpolation du projet reste **désactivée**.
- **Reason** : le même schéma servira à l'interpolation des joueurs distants (M3).
- **Date** : 2026-09-26

## D-008 — Bindings par défaut dans le code, en keycodes physiques
- **Problem** : les bindings QWERTY logiques (`W`) ne correspondent pas à ZQSD sur AZERTY. Le MCP Godot-AI ne sait créer que des keycodes logiques, et éditer `project.godot` pendant que l'éditeur est ouvert risque d'être écrasé.
- **Decision** : `Scripts/Core/InputDefaults.cs` enregistre au boot (autoload `Boot`) les actions et leurs événements par défaut en **physical keycodes** + manette. Une action déjà liée (Project Settings ou override utilisateur futur) n'est pas modifiée.
- **Consequences** : les actions n'apparaissent pas dans l'onglet Input Map de l'éditeur. Le futur menu de rebinding écrira des overrides dans `user://`.
- **Date** : 2026-09-26

## D-009 — Tests in-engine headless
- **Problem** : les comportements de mouvement dépendent de la vraie physique Jolt ; des tests xUnit hors moteur ne la reproduisent pas.
- **Decision** : runner maison `Scenes/Tests/TestRunner.tscn` (réflexion sur `[Test]`, sandbox par test, code de sortie 0/1, rapport JSON `user://test_results.json`, lignes `METRIC`). Lancement : `tools/run_tests.ps1` / `.sh`.
- **Reason** : les métriques de PARKOUR_METRICS sont **mesurées**, pas supposées.
- **Consequences** : le code de test est compilé dans l'assembly du jeu (à exclure des builds release).
- **Date** : 2026-09-26

## D-010 — Resynchronisation du flag « on floor » au rollback
- **Problem** : `CharacterBody3D` garde un flag privé « était au sol » qui décide du snap au tick suivant, sans setter. Après un `RestoreState`, il peut contredire le snapshot → le replay diverge (6 cm mesurés).
- **Decision** : `RestoreState` fait un micro-déplacement sonde (vers le sol si grounded, vers le haut sinon), puis replace le corps à la position exacte du snapshot.
- **Reason** : garantir le contrat de reconciliation ; couvert par `ReplayFromSnapshotIsDeterministic` (qui force volontairement le flag contraire).
- **Date** : 2026-09-26

## D-011 — Greybox en CSG généré par script
- **Decision** : la Movement Gym est générée par `tools/generators/gen_movement_gym.py` (CSGBox3D + `use_collision`, textures prototypes world-triplanar).
- **Reason** : dimensions exactes et reproductibles, alignées sur les métriques.
- **Alternatives** : édition manuelle (dérive des mesures), GridMap (moins flexible pour les rampes).
- **Consequences** : pour les maps compétitives (M6), le CSG sera converti en meshes statiques + collisions simples (coût CPU du CSG).
- **Date** : 2026-09-26

## D-012 — Un saut exige un nouvel appui
- **Decision** : maintenir Saut ne déclenche pas de bunny-hop automatique à l'atterrissage (front montant requis, bufferisé 0.12 s).
- **Reason** : contrôle joueur > automatisme ; évite des sauts involontaires en poursuite.
- **Date** : 2026-09-26

## D-013 — Sprint = bouton maintenu + momentum progressif — **Superseded by D-015**
- **Problem** : Mirror's Edge construit la vitesse automatiquement en courant droit ; le brief demande walk / run / sprint.
- **Decision** : sprint maintenu (Shift / L3), accélération lente au-dessus de la vitesse de course (3 m/s² → 0.87 s pour 95 %), décélération douce (8 m/s²) quand on relâche, perte de momentum proportionnelle aux virages serrés.
- **Alternatives** : sprint automatique après X s de course droite ; toggle.
- **Consequences** : question ouverte à trancher en playtest (prompt ChatGPT fourni dans le rapport M0/M1).
- **Date** : 2026-09-26

## D-014 — FOV horizontal
- **Decision** : `Camera3D.keep_aspect = KEEP_WIDTH`, FOV horizontal 95° par défaut (70–120 réglable).
- **Reason** : perception de vitesse cohérente quel que soit le ratio d'écran ; les joueurs de FPS raisonnent en FOV horizontal.
- **Date** : 2026-09-26

## D-015 — Progression automatique walk → run → sprint (aucune touche marche / sprint)
- **Problem** : la direction du projet veut un modèle de momentum à la Mirror's Edge : la vitesse se construit en maintenant « avancer ».
- **Decision** : suppression des actions `walk` et `sprint` (et des bits d'`InputButtons`). La vitesse cible dépend de la **direction** de l'input (`LocomotionMath.TopSpeedFor`) : cône avant (dot ≥ 0.7) → sprint 8 m/s ; strafe → 4.5 ; arrière → 3.0 ; stick < 60 % → marche. L'accélération se fait par paliers (30 / 12 / 2.2 m/s²) : 0 → marche 0.08 s, → course 0.37 s, → 95 % sprint 1.33 s. Les virages font perdre la vitesse au-delà de la course (0.3 par radian).
- **Reason** : une bonne ligne est récompensée ; il n'y a pas de touche à gérer en poursuite ; c'est la sensation visée.
- **Alternatives** : sprint maintenu (D-013), toggle.
- **Consequences** : au clavier, il est impossible de *rester* à vitesse de marche (seule la manette peut marcher via la course du stick). Or le pilier « discrétion » demande de pouvoir ralentir pour ne pas être identifié → **question ouverte** à trancher avant M4 (voir KNOWN_ISSUES KI-09).
- **Date** : 2026-09-26

## D-016 — Quatre paliers de réception
- **Decision** : Soft < 3.20 m ≤ Medium < 5.12 m ≤ Heavy < 6.45 m ≤ Deadly (hauteur mesurée depuis l'apex). Chaque palier a sa rétention de vitesse, sa durée de récupération et son multiplicateur de déplacement (`MovementTuning`, sous-groupes Medium / Heavy). Deadly = mort → respawn.
- **Reason** : consigne de la direction du projet ; la consigne laissait une zone 6.40–6.45 m non attribuée, comptée en Heavy.
- **Consequences** : le landing roll (M2) devra rétrograder Medium / Heavy.
- **Date** : 2026-09-26

## D-017 — Rejet du « SFX_PACK » fourni
- **Problem** : un pack de 8 166 fichiers (828 Mo) a été ajouté au projet. Son contenu (classe UE3 `SoundNodeWave`, chapitres `A_SP01`–`A_SP09`, `A_TimeTrial`, `A_Ambience_Stormdrains`, cinématique `A_CS_SP04_RBThrowingFaith`, voix `A_Character_Female_01`, armes du jeu) montre qu'il s'agit d'audio **extrait de Mirror's Edge (EA / DICE, 2008)**.
- **Decision** : aucun fichier de ce pack n'est utilisé. Le dossier est exclu de Git (`.gitignore`) et de l'import Godot (`SFX_PACK/.gdignore`). Les fichiers restent sur le disque local du propriétaire.
- **Reason** : propriété intellectuelle d'EA ; interdiction explicite du brief (aucun asset d'un jeu existant) ; un push public reviendrait à redistribuer des contenus protégés.
- **Alternatives** : packs sous licence (Sonniss GDC bundles, packs commerciaux), NOIZAI, enregistrements maison.
- **Date** : 2026-09-26

## D-018 — Audio de mouvement : événements de simulation + placeholders procéduraux
- **Decision** : les pas sont émis par le **motor** (`MotorState.StrideCycle`, un pas à chaque passage d'un entier ; foulée de 0.75 m en marche à 1.9 m en sprint). Le head bob utilise la même phase, donc le son et l'image sont synchronisés, et c'est réplicable en réseau. `PlayerAudio` (présentation) joue les événements via `SfxEvent` (variantes + randomisation) regroupés dans un `MovementAudioBank`. La surface vient de la métadonnée `surface` du collider (`SurfaceType`). Les sons provisoires sont **générés procéduralement** (`tools/generators/gen_placeholder_sfx.py`) : originaux et sans licence tierce.
- **Consequences** : remplacer les sons = éditer `Resources/Audio/DefaultMovementAudio.tres`.
- **Date** : 2026-09-26

## D-019 — Air control plafonné à 2.5 m/s (hors momentum d'entrée)
- **Problem** : avec un plafond à 5.5 m/s, un saut depuis la marche gagnait de la vitesse en l'air (3.15 m au lieu d'environ 1.5 m) : le momentum pouvait se fabriquer en l'air.
- **Decision** : vitesse horizontale atteignable en l'air = max(vitesse d'entrée, 2.5 m/s).
- **Date** : 2026-09-26

## D-020 — Architecture de la traversée : moves sans état + état unique dans MotorState
- **Problem** : les mouvements de parkour doivent rester rollbackables (M3) et ne pas transformer `PlayerMotor` en god class.
- **Decision** : `Veilrun.Traversal` contient des moves **statiques sans état** (`SlideMove`, `RollMove`, `ObstacleMoves`, `LedgeHangMove`, `WallRunMove`, `WallClimbMove`) coordonnés par `TraversalContext`. Tout l'état persistant est dans `MotorState` (kind, temps, paramètres de chemin, normales, cooldowns, buffers, posture). `PlayerMotor` expose des services internes (`MoveBody`, `HandleLanding`, `CanStandAt`, `SyncBodyContactState`…).
- **Alternatives** : une machine à états d'objets `TraversalState` (plus classique mais l'état se retrouve éparpillé dans des instances → rollback plus difficile).
- **Consequences** : ajouter un move = champs dans `MotorState` + fichier de move + branchement (checklist dans PARKOUR.md §3).
- **Date** : 2026-09-26

## D-021 — Moves scriptés (vault, mantle, ledge climb) sans collision pendant le chemin
- **Decision** : la position suit `TraversalContext.EvaluatePath` (montée avec ease-out puis translation) ; le dégagement est validé **avant** le départ par des capsules de test (point de passage + arrivée).
- **Reason** : trajectoire parfaitement lisible et déterministe, sans accrochage sur les coins ; coût nul pendant le move.
- **Consequences** : un obstacle *mobile* apparu pendant le chemin serait traversé (monde statique pour l'instant, voir KNOWN_ISSUES).
- **Date** : 2026-09-26

## D-022 — Sonde frontale = box cast + rayons, avec budget de queries
- **Decision** : une boîte fine de la largeur de la capsule (de 0.06 m jusqu'au-dessus de la hauteur de saisie) est balayée vers l'avant (`CastMotion` + `GetRestInfo`), puis des rayons lisent le sommet, la profondeur debout et le bord opposé. Rayons latéraux **doublés** (0.9 m et 1.5 m) pour le wall run. Budget 8 queries par tick (mesuré : 7).
- **Reason** : jamais de mécanique critique sur un seul rayon ; la boîte voit tout obstacle dans la largeur du corps, à toute hauteur.
- **Date** : 2026-09-26

## D-023 — Crouch / crouch-walk comme mode discret
- **Decision** : maintenir accroupi au sol = capsule de 1.1 m, 2 m/s ; se relever exige de la place au-dessus. Donne aux joueurs clavier un moyen de rester lent (réponse partielle à KI-09) et un mouvement de franchissement (tunnels de 1.4 m).
- **Date** : 2026-09-26

## D-024 — Retournement du wall kick côté présentation
- **Decision** : le motor émet `MotorEvents.TurnAround` ; `PlayerInput.BeginTurn(π, 0.22 s)` fait tourner le yaw côté client. Le yaw revient ensuite au serveur via les `InputCommand` suivantes.
- **Reason** : la vue appartient à l'input du client ; le serveur reste autoritaire sur la position.
- **Date** : 2026-09-26

## D-025 — Hauteurs minimales de traversée à 0.10 m (step-up)
- **Problem** : la capsule ne franchit que 0.10 m ; avec des moves commençant à 0.35 m, une bordure de 0.15–0.35 m **bloquait** un runner lancé.
- **Decision** : vault et mantle commencent à 0.10 m ; jusqu'à 0.5 m, le quick climb conserve 100 % de la vitesse (« step-up »). Couvert par le test `LowObstaclesNeverBlockARunner`.
- **Date** : 2026-09-26

## D-026 — La sonde de resynchronisation « au sol » porte aussi loin que le floor snap
- **Problem** : les moves scriptés se terminent 2 cm au-dessus de la surface ; la sonde de 1.6 cm (D-010) manquait le sol, le résultat dépendait alors de l'historique → divergence de 15 cm détectée par le test de parcours.
- **Decision** : sonde vers le bas de `FloorSnapLength + 0.05` m.
- **Date** : 2026-09-26

## D-027 — Points de spawn de dev (F6)
- **Decision** : les cartes de test déclarent des `Marker3D` dans le groupe `dev_spawn` ; F6 (build debug) les parcourt dans l'ordre alphabétique et en fait le nouveau point de respawn (F4).
- **Date** : 2026-09-26
