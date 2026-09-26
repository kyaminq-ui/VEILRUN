# VEILRUN — Audit technique initial (2026-09-26)

## État trouvé
| Élément | Constat |
|---|---|
| Moteur | Godot **4.7.2-stable Mono** (vérifié via MCP), Forward+, Jolt Physics, D3D12 |
| .NET | SDK 9.0.318 installé ; `Godot.NET.Sdk` 4.7.2 → cible **net8.0** |
| C# | **aucun** fichier `.cs`, `.csproj` ou `.sln` |
| Scènes | **aucune** (pas de main scene) |
| Git | **pas de dépôt** ; `.gitignore` minimal (`.godot/`, `/android/`) |
| Plugins | `addons/godot_ai` v4.2.3 (MCP, GDScript, éditeur) + un dossier de backup `.godot_ai_update` (déjà ignoré par son propre `.gitignore`) |
| Assets | 2 textures de grille prototypes (`grid_textures/`, provenance non documentée) |
| Docs | aucune |

## Réutilisable
Configuration projet (Jolt, D3D12, stretch), plugin MCP, textures de grille (dev uniquement).

## Corrigé / créé
Dépôt Git + LFS, `.gitignore` / `.gitattributes` / `.editorconfig` complets, projet C# et solution, arborescence cible, autoloads, bindings AZERTY-safe, motor M1 complet, caméra, outils de debug, gym de métriques, 18 tests automatisés, 15 documents.

## Manquant (par ordre de priorité)
1. Validation humaine du ressenti (M1).
2. Framework de traversée + mouvements M2.
3. Spike réseau (serveur dédié, prediction et reconciliation sous latence).
4. Persistance des settings, audio placeholder.
5. Export presets, exclusion des tests et du helper MCP en release.

## Risques principaux
| Risque | Impact | Mitigation |
|---|---|---|
| Traversée (vault / mantle / wall-run) difficile à rendre rollbackable | bloque M3 | états de traversée déterministes dans `MotorState`, spike réseau juste après M2-a |
| Divergence flottante client Windows ↔ serveur Linux | corrections fréquentes | seuil de correction + lissage visuel ; mesurer tôt |
| Coût CPU des probes de parkour × 8 joueurs sur le serveur | tick serveur > budget | probes centralisées, budget ≤ 6 casts par runner et par tick |
| Scope (le brief est très large) | dilution | tranches verticales strictes, ROADMAP |
| Licences des assets IA (Suno, Tripo…) | risque légal | ASSET_PROVENANCE systématique |
| Le « feel » dépend du tuning, qui ne se valide qu'en jouant | M1 non clôturable par tests seuls | session de playtest dédiée, tuning en data |
