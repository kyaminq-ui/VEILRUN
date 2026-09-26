# VEILRUN — Art Bible (provisoire)

> ⚠️ Direction artistique **non validée** : c'est une décision majeure qui reviendra au directeur du projet avant M8. Ce document fixe les contraintes fonctionnelles, qui sont, elles, déjà valables.

## 1. Objectif
**Ville moderne lisible à haute vitesse.** Silhouettes, matériaux lisibles, formes fortes, grands contrastes, landmarks, profondeur, obstacles lisibles. Pas de photoréalisme bruité.

## 2. Interdits
Pas de code couleur blanc / rouge de Mirror's Edge. Pas de reprise de personnages, noms, cartes, textures, logos, UI ou designs d'Assassin's Creed ou de Mirror's Edge.

## 3. Palette de départ (provisoire)
Béton clair · verre fumé · métal sombre · accents **cyan**, **ambre**, **violet électrique** · végétation contrôlée · néons fonctionnels.

## 4. Lisibilité du parkour
- Les surfaces traversables sont identifiables **par leur construction** (rebords épais, rambardes à hauteur de vault, bords chanfreinés, matériau de toiture distinct), pas par une couleur magique.
- Un accent de wayfinding **optionnel**, basé sur la lumière et la composition, avec couleur d'accent personnalisable, mode haut contraste et palettes adaptées au daltonisme.
- Hauteurs d'obstacles alignées sur `PARKOUR_METRICS.md` (vault 0.9–1.2 m, mantle 1.2–2.0 m…) : l'œil apprend les hauteurs.

## 5. Greybox (M0–M6)
Textures prototypes à grille (lignes majeures 1 m, mineures 0.25 m) · gris foncé = sol · orange = obstacles et éléments de parkour · bleu = rampes et pentes · gris-bleu = murs. Aucune map compétitive ne commence avec de beaux assets.

## 6. Pipeline d'assets
Référence → Lychee / ChatGPT → concept → TripoAI si pertinent → Blender (+ MCP) → cleanup → retopo → UV → matériaux → LOD → collision → glTF → Godot. Un mesh IA n'est **jamais** production-ready sans inspection (topologie, normales, échelle, UV, polycount, matériaux, pivot, collision, LOD, licence). Pas de mesh IA brut pour le personnage joueur, les collisions critiques, les hero assets ou les surfaces de parkour.

## 7. Kits modulaires
Grille 0.5 m / 1 m, étage de 4.0 m, murs de 0.25–0.3 m, portes 1.0 × 2.1 m (1.4 × 2.4 m pour les routes principales). Collisions simplifiées et intentionnelles, particulièrement propres sur les surfaces de wall-run, vault, mantle, ledge et slide.
