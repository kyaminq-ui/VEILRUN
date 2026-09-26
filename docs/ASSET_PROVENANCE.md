# VEILRUN — Asset Provenance

Toute ressource non créée à la main dans ce dépôt est listée ici **avant** d'être utilisée dans un build distribué.
Statuts : `dev-only` (jamais livré) · `placeholder` · `candidate` · `final` · `rejected`.

| Asset | Source | Outil | Date | Licence | Modifications | Statut |
|---|---|---|---|---|---|---|
| `Dev/Textures/Grid/PNG/Dark_Floor/texture_03.png` (+ `.svg`, `.swf`) | Présent à l'initialisation du projet. Nommage et format identiques au pack **Kenney « Prototype Textures »** — **à confirmer par le propriétaire** | — | avant le 2026-09-26 | **Non vérifiée** (Kenney = CC0 si confirmé) | déplacé de `res://grid_textures/` | dev-only |
| `Dev/Textures/Grid/PNG/Orange_Wall/texture_04.png` (+ `.svg`, `.swf`) | idem | — | avant le 2026-09-26 | **Non vérifiée** | idem | dev-only |
| `icon.svg` | icône par défaut de Godot | Godot | 2026-09-26 | MIT (Godot) | aucune | placeholder |
| `Assets/Audio/SFX/Placeholder/Movement/*.wav` (31 fichiers : pas béton / métal, saut, réceptions ×4 paliers, respiration ×2, vent) | généré dans ce dépôt par `tools/generators/gen_placeholder_sfx.py` (synthèse de bruit / sinus, seed fixe) | Python + numpy | 2026-09-26 | œuvre originale du projet | — | placeholder |
| `SFX_PACK/` (8 166 fichiers) | **audio extrait de Mirror's Edge (EA / DICE)** — voir D-017 | — | 2026-09-26 | **propriété d'EA — non utilisable** | exclu de Git et de Godot | **rejected** |
| `addons/godot_ai/` | plugin « Godot AI » v4.2.3 (MCP Godot-AI by dlight) | — | 2026-09-26 | voir `addons/godot_ai/LICENSE` | aucune | outil éditeur (hors build) |

## Règles
- Toute génération IA (TripoAI, Lychee, Suno, NOIZAI) : noter l'outil, le plan d'abonnement au moment de la génération et les conditions commerciales en vigueur, puis archiver le prompt dans le dossier source de l'asset.
- Aucun asset issu de jeux existants, même temporairement.
- Les fichiers `.swf` des textures prototypes sont inutiles au projet et pourront être supprimés après confirmation.
