# VEILRUN — Parkour Metrics

> **Source de vérité** pour le level design. Aucune map finale ne doit être construite avant stabilisation de ces valeurs.
> Les valeurs **mesurées** proviennent de la suite de tests automatisés (`tools/run_tests.ps1`, lignes `METRIC`) exécutée
> sur Jolt Physics à 60 Hz avec `Resources/Movement/DefaultMovementTuning.tres`. Toute modification du tuning doit être suivie
> d'une ré-exécution des tests et d'une mise à jour de ce document.

Dernière mesure : 2026-09-26 — Godot 4.7.2 mono — tuning par défaut (v0.1.1).

Légende statut : ✅ mesuré par test · 📐 dérivé du tuning · 🎯 cible de design (pas encore de mécanique) · ❓ à définir

## 1. Corps du runner

| Métrique | Valeur | Statut |
|---|---|---|
| Capsule — rayon / hauteur | 0.35 m / 1.80 m | 📐 |
| Hauteur des yeux | 1.65 m | 📐 |
| Angle de sol max (marchable) | 46° | ✅ 30° marchable, 55° non marchable |
| Floor snap (descente collée) | 0.35 m | 📐 |
| Step-up (marche montante) | **aucune logique dédiée** — la capsule franchit seule **0.10 m max** (0.15 m bloque) — voir règle §6 | ✅ |

## 2. Vitesses — progression automatique (aucune touche marche/sprint)

Maintenir **avancer** fait passer progressivement marche → course → sprint (modèle « momentum » à la Mirror's Edge).
La vitesse max dépend de la direction de l'input.

| Métrique | Valeur | Statut |
|---|---|---|
| Marche / course / sprint | 2.2 / 5.5 / 8.0 m/s | ✅ |
| Accélération par palier | 30 / 12 / 2.2 m/s² | 📐 |
| Temps 0 → marche | **0.083 s** | ✅ |
| Temps 0 → course | **0.367 s** | ✅ |
| Temps 0 → 95 % sprint | **1.333 s** | ✅ |
| Vitesse max en strafe pur | 4.5 m/s | ✅ |
| Vitesse max en marche arrière | 3.0 m/s | ✅ |
| Cône « sprint » (dot input/regard) | ≥ 0.7 (≈ ±45°) | 📐 |
| Stick analogique < 60 % | plafonné à la marche | 📐 |
| Virage 90° en sprint — vitesse minimale | 7.38 m/s (perte 0.3 × excès/rad) | ✅ |
| Freinage sans input (8 → 0) | ≈ 0.36 s | 📐 |
| Cadence de pas en sprint | 4.0 pas/s (foulée 1.9 m ; 0.75 m en marche) | ✅ |

## 3. Saut

| Métrique | Valeur | Statut |
|---|---|---|
| Hauteur de saut (apex) | **1.200 m** | ✅ (écart mesuré < 1 mm) |
| Temps jusqu'à l'apex | 0.36 s | 📐 |
| Temps de vol (sol plat) | 0.650 s | ✅ |
| Gravité montée / descente | 18.5 / 25.9 m/s² | 📐 |
| Coyote time | 0.12 s | ✅ (6 ticks OK, 10 ticks refusé) |
| Jump buffer | 0.12 s | ✅ |
| Air control : vitesse atteignable en l'air | max(vitesse d'entrée, 2.5 m/s) | ✅ |
| **Saut en marche — distance (même niveau)** | **1.67 m** | ✅ |
| **Saut en course — distance (même niveau)** | **3.67 m** | ✅ |
| **Saut en sprint — distance (même niveau)** | **5.33 m** | ✅ |

### Gaps recommandés (bord à bord, même hauteur)

| Usage | Gap max | Marge |
|---|---|---|
| Saut facile (course) | ≤ 3.0 m | ~0.65 m |
| Saut engagé (sprint lancé) | ≤ 4.5 m | ~0.8 m |
| 5.0 m + | **infranchissable en M1** — réservé aux mouvements M2 (slide-jump, wall-run, ledge grab) | — |

La **gym** contient des gaps de 2 / 3 / 4 / 5 / 6 m : 5 et 6 m doivent échouer en M1 (vérification de lisibilité des limites).

## 4. Réceptions / chutes

Hauteur de chute mesurée **depuis l'apex**. Paliers imposés par la direction du projet (2026-09-26).

| Palier | Hauteur de chute | Impact | Effet | Statut |
|---|---|---|---|---|
| **Soft** | < 3.20 m | < 12.88 m/s | aucun — le momentum est conservé | ✅ |
| **Medium** | 3.20 – 5.12 m | 12.88 – 16.29 m/s | vitesse ×0.75, déplacement ×0.8 pendant 0.2 s | ✅ |
| **Heavy** | 5.12 – 6.45 m | 16.29 – 18.29 m/s | vitesse ×0.3, déplacement ×0.45 pendant 0.6 s | ✅ |
| **Deadly** | ≥ 6.45 m | ≥ 18.29 m/s | mort → respawn | ✅ |
| Landing roll | 🎯 M2 — doit rétrograder Medium/Heavy d'un palier | | | ❓ |

> Note : la consigne donnait « Heavy 512–640 cm, Deadly au-delà de 645 cm ». La zone 6.40–6.45 m est comptée en Heavy
> (un seul seuil `DeadlyFallHeight = 6.45`). Modifiable dans `DefaultMovementTuning.tres`.

## 5. Hauteurs d'obstacles (références pour M2)

| Hauteur | Intention | Statut |
|---|---|---|
| ≤ 0.5 m | enjambable / low vault | 🎯 |
| 0.9 – 1.2 m | vault (speed vault) — hauteur « rambarde » | 🎯 |
| 1.2 – 2.0 m | mantle | 🎯 |
| 2.0 – 2.6 m | ledge grab après saut | 🎯 |
| > 2.6 m | infranchissable sans wall-run / wall climb | 🎯 |

Les blocs de la gym (0.5 / 0.9 / 1.2 / 1.5 / 2.0 / 2.5 / 3.0 m) servent à valider ces seuils quand M2 arrivera.

## 6. Architecture / modules (règles de construction)

| Élément | Valeur | Statut |
|---|---|---|
| Unité | 1 unité Godot = 1 m | 📐 |
| Grille de base / majeure | 0.5 m / 1 m (textures de la gym : lignes majeures = 1 m, mineures = 0.25 m) | 📐 |
| Hauteur d'étage (floor-to-floor) | 4.0 m | 🎯 |
| Épaisseur de mur | 0.25 – 0.3 m | 🎯 |
| Porte standard | 1.0 × 2.1 m | 🎯 (présente dans la gym) |
| Porte de route principale | 1.4 × 2.4 m | 🎯 |
| Couloir minimum | 1.2 m (capsule 0.7 m + marge) | 🎯 (présent dans la gym) |
| Couloir de poursuite | ≥ 2.0 m | 🎯 |
| Zone d'atterrissage minimale | 2 × 2 m après un gap ; 3 m de profondeur après un saut en sprint | 🎯 |
| Couverture (cache) | 1.1 – 1.3 m | 🎯 |
| **Escaliers / bordures** | **toujours une collision en rampe ≤ 40°** ; bordure verticale tolérée ≤ 0.10 m | ✅ |
| Collision des surfaces de parkour | boîtes / convexes simples, jamais le mesh visuel | 📐 |

## 7. Movement Gym

Scène : `res://Dev/TestMaps/MovementGym.tscn` — **générée** par `tools/generators/gen_movement_gym.py`
(modifier le script puis le relancer, ne pas éditer la scène à la main).

| Zone | Position | Contenu |
|---|---|---|
| Spawn | (0, 0, 8), face à −Z | |
| A. Gap jumps | x −32…−8 | deck à +2 m, rampe d'accès, gaps 2/3/4/5/6 m |
| B. Heights | x 4…22, z −6 | blocs 0.5 → 3.0 m |
| C. Drop heights | x 36 | rampe (surface **metal**) jusqu'à 12 m, repères 3.2 / 5.12 / 6.45 (seuils Medium / Heavy / Deadly) / 8 / 12 m, deck sommital |
| D. Slopes | x 50…68 | rampes 20° / 35° / 45° / 50° (surface **metal**) |
| E. Murs | x −45…−54 | mur 24 m (wall-run M2), couloir 1.2 m, porte 1.0 × 2.1 m |
