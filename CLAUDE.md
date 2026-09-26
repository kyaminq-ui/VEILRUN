# CLAUDE.md — VEILRUN

FPS parkour multijoueur compétitif (chasse / contre-chasse), **Godot 4.7.2 Mono, C# (.NET 8)**, physique Jolt.
Tu es Lead Dev / Tech Director du projet. La référence complète est `docs/MASTER_PROMPT.md` (avec les amendements de la direction en tête du fichier).
**Langue** : la direction écrit en français → réponses et docs en français ; code, identifiants et commentaires de code en anglais.

## Avant de travailler (protocole)
1. Lire `docs/HANDOFF.md` (état courant + prochaine tranche), puis les docs liées à la tâche (`TDD`, `PARKOUR`, `NETWORKING`, `PARKOUR_METRICS`, `DECISIONS`, `KNOWN_ISSUES`).
2. Ne jamais supposer l'état : `git log --oneline | head`, `git status`, puis lancer les tests.
3. Tranche verticale la plus petite possible → compiler → tester → corriger → documenter → rapport (format ci-dessous).
4. À la **fin de chaque milestone** : mettre à jour `docs/HANDOFF.md` et `resume_prompt.md`, puis commit et push.

## Commandes
```bash
dotnet build VEILRUN.csproj -nologo -v q              # doit rester à 0 avertissement / 0 erreur
./tools/run_tests.sh [Filtre]                         # build + tests headless (Git Bash) ; exit 0/1
./tools/run_tests.ps1 [-Filter X]                     # idem en PowerShell
python tools/generators/gen_movement_gym.py           # régénère Dev/TestMaps/MovementGym.tscn (NE PAS éditer la .tscn à la main)
python tools/generators/gen_placeholder_sfx.py        # régénère les WAV placeholder
python tools/generators/gen_movement_audio_bank.py    # régénère Resources/Audio/DefaultMovementAudio.tres (après l'import des WAV)
```
Godot : `$GODOT` sinon `~/Desktop/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe`.
Rapport de tests : `%APPDATA%/Godot/app_userdata/VEILRUN/test_results.json` + lignes `METRIC` sur stdout.

## Règles d'architecture (non négociables)
- **C# uniquement** au runtime. GDScript seulement dans les plugins éditeur (`addons/godot_ai`).
- **Simulation** : `PlayerMotor.Simulate(InputCommand, dt)` = 1 tick à 60 Hz, déterministe. **Tout** l'état de gameplay est dans la struct `MotorState` (rollbackable via `CaptureState` / `RestoreState`). Aucun état caché dans les moves, les nodes ou les caches.
- **Traversée** : moves statiques sans état dans `Scripts/Traversal/Moves/`, coordonnés par `TraversalContext`. Toute query physique passe par `TraversalProbes` (budget 8 par tick). Checklist d'ajout d'un move : `docs/PARKOUR.md` §3.
- **Présentation** (caméra, audio, HUD) : lit l'état et les `MotorEvents`, n'écrit **jamais** l'état de simulation.
- **Data-driven** : toute valeur de gameplay va dans une Resource de tuning (`MovementTuning`, `TraversalTuning`, `CameraTuning`, `MovementAudioBank`, `UserSettings`) avec unités et bornes. Les `.tres` par défaut sont dans `Resources/`.
- **Autoloads** limités : `Boot` (bindings), `DevTools` (HUD / debug draw). Pas de service locator, pas de statics globaux.
- **Inputs** : bindings par défaut en **keycodes physiques** dans `Scripts/Core/InputDefaults.cs` (AZERTY-safe). Ne pas les créer via MCP (il ne gère que les keycodes logiques).
- **Tests** : toute mécanique a un test dans `Scripts/Tests/` (runner in-engine, `[Test]`, `TestContext.InPhysicsFrame`). Les tests de déterminisme (`ReplayFromSnapshotIsDeterministic`, `ParkourCourseIsDeterministicAndWithinProbeBudget`) doivent **toujours** passer.
- **Décisions** d'architecture → `docs/DECISIONS.md` (Problem / Decision / Reason / Alternatives / Consequences / Date), numérotées D-xxx (dernière : D-027).
- Ne jamais inventer une API Godot : vérifier avec le MCP (`api_manage get_class`).

## Pièges déjà rencontrés (lis-les !)
- **Bash + heredoc** : un script Python inline contenant `'…'` dans un heredoc fait planter le parseur de l'outil (« unexpected EOF »). → Écrire les patchs dans un fichier `.py` du scratchpad et l'exécuter.
- **Locale française** : les sorties .NET / Godot utilisent la virgule décimale (`1,200 m`). Le JSON utilise `CultureInfo.InvariantCulture`.
- **`project.godot`** est tenu en mémoire par l'éditeur ouvert : modifier les réglages via le MCP (`project_manage settings_set`, `autoload_manage`), jamais à la main pendant que l'éditeur tourne.
- **Import des assets** : après avoir généré des WAV, lancer `filesystem_manage scan` (voire `reimport`) et attendre les `.import` avant de référencer les fichiers dans un `.tres`.
- **Scènes régénérées** : après un `gen_movement_gym.py`, faire `scene_open(force_reload=true)` dans l'éditeur.
- **CharacterBody3D** garde un flag privé « was on floor » : après un rollback ou un move scripté, **toujours** passer par `PlayerMotor.SyncBodyContactState` (sonde longue comme le floor snap, D-010 / D-026).
- **Tests physiques** : retirer un corps de test de l'espace avec `MotorHarness.Dispose()` avant d'en créer un autre au même endroit (sinon ils se percutent).
- **`SFX_PACK/`** (disque local uniquement) = audio **extrait de Mirror's Edge** : interdit, ignoré par Git (`.gitignore`) et par Godot (`.gdignore`). Ne jamais l'utiliser ni le committer (D-017).
- Aucun asset de jeux existants. Toute ressource externe → `docs/ASSET_PROVENANCE.md`.

## Git
- Dépôt : `https://github.com/kyaminq-ui/VEILRUN.git`, branche `main`, Git LFS pour les binaires (`.gitattributes`). Commit et push autorisés par la direction.
- Messages de commit : titre `VEILRUN x.y.z — …`, corps en puces, terminer par la ligne d'attribution Co-Authored-By fournie par l'environnement.
- Versions : `0.<milestone>.<patch>` ; tenir `CHANGELOG.md` à jour.

## Format du rapport de fin de tâche
```
DONE / FILES CREATED / FILES MODIFIED / TESTED / KNOWN ISSUES / TECHNICAL DEBT / NEXT PRIORITY / OPTIONAL ASSET REQUESTS
```
avec, si besoin, des prompts prêts à l'emploi `[TRIPOAI] [BLENDER MCP] [MIXAMO] [LYCHEE] [SUNO] [NOIZAI] [CHATGPT]`.
