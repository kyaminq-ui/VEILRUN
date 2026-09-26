# VEILRUN — Known Issues & Technical Debt

Mis à jour : 2026-09-26 (v0.1.1)

## Problèmes connus
| # | Sévérité | Description | Contournement / plan |
|---|---|---|---|
| KI-01 | moyenne | Pas de step-up : une marche verticale de plus de 0.10 m bloque le runner (mesuré : 0.10 m franchi, 0.15 m bloquant ; limite théorique r·(1−cos 46°) = 0.107 m) | règle de level design : collisions en rampe pour escaliers et bordures (PARKOUR_METRICS §6). Low-step / mantle en M2 |
| KI-02 | basse | `UserSettings` n'est pas persisté (réinitialisé à chaque lancement) | persistance `user://settings.cfg` + menu options (clôture M1) |
| KI-03 | basse | Le réglage *Motion Blur* est stocké mais sans effet : Godot 4.7 n'a pas de motion blur intégré | compositor effect à évaluer en M8, ou retrait de l'option |
| KI-04 | basse | Les actions d'input sont créées en code (D-008) : invisibles dans l'onglet Input Map de l'éditeur | menu de rebinding + overrides `user://` |
| KI-05 | info | Le « frame ms » du HUD provient de `Performance.TIME_PROCESS`, échantillonné par Godot environ 1 fois par seconde | ajouter un graphe de frame time en M3 |
| KI-06 | info | Le déplacement MCP des textures a expiré ; elles ont été déplacées sur disque avec leurs `.import` (UID conservés) | aucun : l'éditeur a réimporté |
| KI-07 | basse | Le mesh placeholder du runner ne tourne pas avec le yaw (capsule symétrique) | remplacé par le vrai personnage en M7 |
| KI-08 | info | La licence des textures de grille n'est pas vérifiée | voir ASSET_PROVENANCE |
| KI-09 | **design** | Sans touche de marche (D-015), un joueur clavier ne peut pas se maintenir à vitesse de marche, alors que le pilier discrétion / identification en a besoin | options : maintenir « accroupi » = pas discret, ou relâcher / taper avancer, ou une touche « composure ». À trancher avant M4 |
| KI-10 | basse | Sons de mouvement = placeholders procéduraux (qualité prototype) | remplacement par NOIZAI / packs sous licence (prompts dans le rapport) |
| KI-11 | info | `SFX_PACK/` (audio Mirror's Edge) présent sur le disque local, ignoré par Git et par Godot (D-017) | à supprimer localement quand le propriétaire le décide |

## Dette technique
| # | Description | Échéance |
|---|---|---|
| TD-01 | `Runner` dépend directement de `PlayerInput` → introduire `IInputSource` (local / réseau / replay) | M3 |
| TD-02 | Le code de test (`Scripts/Tests`) est compilé dans l'assembly du jeu → l'exclure des exports release (condition MSBuild ou assembly séparée) | avant le premier export |
| TD-03 | L'autoload `_mcp_game_helper` (GDScript, plugin Godot-AI) est chargé à l'exécution → le retirer ou le conditionner dans les exports release | avant le premier export |
| TD-04 | `MotorHarness.Create` duplique la configuration de capsule de `Runner.ApplyCollisionShape` → factoriser si une 3ᵉ copie apparaît | opportuniste |
| TD-05 | Aucun `export_presets.cfg` ni pipeline de build | M3 (serveur dédié) |
| TD-06 | Le contrat de déterminisme ne couvre ni les plateformes mobiles ni les objets dynamiques | M3 / M6 |
| TD-07 | Crouch est lié (Ctrl / C) mais n'a pas encore d'effet | M2 (slide) |
