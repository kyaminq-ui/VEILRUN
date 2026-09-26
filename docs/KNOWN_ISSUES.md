# VEILRUN — Known Issues & Technical Debt

Mis à jour : 2026-09-26 (v0.2.0)

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
| KI-09 | **design** | Sans touche de marche (D-015), un joueur clavier ne peut pas se maintenir à vitesse de *marche debout* | partiellement résolu : le crouch-walk (2 m/s) est un mode discret (D-023). Reste à décider en M4 si « se fondre dans la foule » exige une marche debout (touche « composure » ?) |
| KI-10 | basse | Sons de mouvement = placeholders procéduraux (qualité prototype) | remplacement par NOIZAI / packs sous licence (prompts dans le rapport) |
| KI-11 | info | `SFX_PACK/` (audio Mirror's Edge) présent sur le disque local, ignoré par Git et par Godot (D-017) | à supprimer localement quand le propriétaire le décide |
| KI-12 | moyenne | Les moves scriptés ne testent pas les collisions pendant le chemin (D-021) : un obstacle mobile pourrait être traversé | valider le chemin par un shape cast si des objets dynamiques arrivent (portes, M5+) |
| KI-13 | basse | Le vault part toujours perpendiculairement à l'obstacle (direction = −normale) : une approche en biais « se redresse » | vault orienté selon l'approche, avec bord opposé projeté (M2+) |
| KI-14 | basse | Le mantle ne vérifie pas un surplomb au-dessus de la face pendant la montée (seulement l'arrivée) | ajouter une capsule de test au sommet de la montée si le budget le permet |
| KI-15 | basse | Le shimmy s'arrête aux angles (pas de corner transition) ; pas de drop-to-ledge volontaire | post-M2 |
| KI-16 | info | `IntersectRay` / `GetRestInfo` allouent un `Dictionary` par query (API Godot C#) → léger GC pendant les probes | mesurer en M3 avec 8 runners ; mettre en cache ou passer par `PhysicsServer3D` si besoin |
| KI-17 | basse | La roulade est représentée par une plongée de caméra de 40° (pas de rotation complète, choix de confort) | à revoir avec l'animation FP en M7 |

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
