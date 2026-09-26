# VEILRUN — Parkour Metrics

> **Source de vérité** pour le level design. Aucune map finale ne doit être construite avant stabilisation de ces valeurs.
> Les valeurs **mesurées** proviennent de la suite de tests automatisés (`tools/run_tests.ps1`, lignes `METRIC`) exécutée
> sur Jolt Physics à 60 Hz avec `Resources/Movement/DefaultMovementTuning.tres`. Toute modification du tuning doit être suivie
> d'une ré-exécution des tests et d'une mise à jour de ce document.

Dernière mesure : 2026-09-26 — Godot 4.7.2 mono — tuning par défaut (v0.2.0, M2).

Légende statut : ✅ mesuré par test · 📐 dérivé du tuning · 🎯 cible de design (pas encore de mécanique) · ❓ à définir

## 1. Corps du runner

| Métrique | Valeur | Statut |
|---|---|---|
| Capsule — rayon / hauteur | 0.35 m / 1.80 m | 📐 |
| Hauteur des yeux | 1.65 m | 📐 |
| Angle de sol max (marchable) | 46° | ✅ 30° marchable, 55° non marchable |
| Floor snap (descente collée) | 0.35 m | 📐 |
| Marche montante | capsule seule : **0.10 m** ; au-delà : **step-up automatique** jusqu'à 0.5 m sans perte de vitesse (M2) | ✅ |

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
| 5.0 – 5.7 m | slide jump (5.74 m mesuré) | ~0.05 m — très engagé |
| > 5.7 m | wall run (12.3 m de l'appel à la réception) ou chemin vertical | — |

La **gym** contient des gaps de 2 / 3 / 4 / 5 / 6 m. 5 m se passe en slide jump ; 6 m reste infranchissable à plat (volontaire).

## 4. Réceptions / chutes

Hauteur de chute mesurée **depuis l'apex**. Paliers imposés par la direction du projet (2026-09-26).

| Palier | Hauteur de chute | Impact | Effet | Statut |
|---|---|---|---|---|
| **Soft** | < 3.20 m | < 12.88 m/s | aucun — le momentum est conservé | ✅ |
| **Medium** | 3.20 – 5.12 m | 12.88 – 16.29 m/s | vitesse ×0.75, déplacement ×0.8 pendant 0.2 s | ✅ |
| **Heavy** | 5.12 – 6.45 m | 16.29 – 18.29 m/s | vitesse ×0.3, déplacement ×0.45 pendant 0.6 s | ✅ |
| **Deadly** | ≥ 6.45 m | ≥ 18.29 m/s | mort → respawn | ✅ |
| **Landing roll** | Medium ou Heavy + accroupi pressé ≤ 0.35 s avant l'impact | | aucune pénalité, vitesse ×0.9 (min 4 m/s), roulade 0.55 s. **Ne sauve jamais Deadly.** Heavy : 7.2 m/s avec roulade contre 2.4 sans | ✅ |

> Note : la consigne donnait « Heavy 512–640 cm, Deadly au-delà de 645 cm ». La zone 6.40–6.45 m est comptée en Heavy
> (un seul seuil `DeadlyFallHeight = 6.45`). Modifiable dans `DefaultMovementTuning.tres`.

## 5. Mouvements de parkour (M2) — valeurs mesurées

### 5.1 Obstacles face au runner (hauteur depuis les pieds)

| Hauteur | Obstacle fin (≤ 1.2 m de profondeur) | Obstacle épais (on peut se tenir dessus) | Statut |
|---|---|---|---|
| < 0.10 m | franchi par la capsule | franchi par la capsule | ✅ |
| 0.10 – 0.5 m | vault | **step-up** (aucune perte de vitesse) | ✅ |
| 0.5 – 1.05 m | vault | **quick climb** (vitesse ×0.85 mesurée) | ✅ |
| 1.05 – 1.3 m | vault | mantle | ✅ |
| 1.3 – 2.0 m | — (mur fin trop haut) | **mantle** depuis le sol (1.6 m : 0.60 s) | ✅ |
| 2.0 – 2.35 m au-dessus des pieds **en l'air** | — | ledge grab (suspension) | ✅ |
| jusqu'à **3.75 m** depuis le sol | — | **wall climb** (course verticale) → ledge grab / mantle | ✅ (4.0 m : échec) |
| > 3.75 m | — | wall kick uniquement | ✅ |

| Mouvement | Valeur | Statut |
|---|---|---|
| Vault d'une rambarde de 1.0 m en sprint | 0.32 s, vitesse conservée à **95 %** | ✅ |
| Vitesse minimale de vault | 1.0 m/s (sortie ≤ vitesse d'entrée + 2 m/s) | 📐 |
| Portée de détection d'obstacle | 0.35 m + 0.08 s × vitesse (≈ 1.0 m en sprint) | 📐 |
| Suspension : pieds sous le rebord | 1.95 m | ✅ |
| Shimmy | 1.3 m/s | ✅ |
| Ledge climb | 0.6 s | 📐 |
| Wall climb | 6.5 m/s décroissant sur 0.5 s | 📐 |
| Wall kick (saut en wall climb ou dos + saut en suspension) | 5 m/s vers l'arrière + 5.5 m/s vers le haut, vue retournée en 0.22 s | ✅ |

### 5.2 Wall run

| Métrique | Valeur | Statut |
|---|---|---|
| Vitesse minimale / angle d'entrée max | 5 m/s / 40° par rapport au mur | 📐 |
| Durée max | 1.3 s | ✅ |
| Hauteur des pieds max (appel au sol) | 2.94 m | ✅ |
| Distance de l'appel à la réception | **12.3 m** (contre 5.33 m en saut sprint) | ✅ |
| Wall jump | tangente ×0.95 + 5 m/s hors du mur + 6.2 m/s vers le haut ; même mur interdit pendant 0.6 s | ✅ |

### 5.3 Slide / crouch

| Métrique | Valeur | Statut |
|---|---|---|
| Déclenchement | accroupi pressé à ≥ 5 m/s | 📐 |
| Glissade à plat | **6.2 m en 1.1 s** (boost +1 m/s, friction 6 m/s²) | ✅ |
| Slide jump | **5.74 m** | ✅ |
| Hauteur libre sous une barre (slide) | **1.0 m** (capsule 0.9 m) | ✅ |
| Crouch-walk | 2.0 m/s, hauteur libre 1.4 m (capsule 1.1 m) | ✅ |
| Land-into-slide | accroupi bufferisé + réception Soft à ≥ 5 m/s | 📐 |

### 5.4 Budget

| Métrique | Valeur | Statut |
|---|---|---|
| Queries physiques max par runner et par tick | **7** mesuré (budget 8) | ✅ |
| Rejeu déterministe sur un parcours vault → slide → mantle → wall run | erreur 0.000 m, 180 checkpoints dont ≥ 5 en plein mouvement | ✅ |

### 5.5 Règles de level design issues de M2
- **Rambardes vaultables** : collision ≥ 0.10 m d'épaisseur, 0.1–1.3 m de haut, ≤ 1.2 m de profondeur ; prévoir 1.5 m libres derrière.
- **Rebords saisissables** : face verticale (|normal.y| < 0.35), dessus horizontal ; 0.7 m de dégagement sous le rebord pour le corps suspendu.
- **Murs de wall run** : ≥ 8 m de long, ≥ 3.5 m de haut ; métadonnée `wallrun = false` sur un collider pour l'interdire (vitres, surfaces sales, zones de fairness).
- **Couloir wall-to-wall** : 3.5–4.5 m entre les faces.
- **Hauteurs signatures** : 1.0 m (vault), 1.6 m (mantle), 3.0 m (saut → rebord), 3.5 m (wall climb), > 4 m (bloquant).

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
| E. Murs | x −45…−54 | mur 24 m, couloir 1.2 m, porte 1.0 × 2.1 m |
| F. **Parkour Lab (M2)** | x 80…155 | rambardes 0.5 / 1.0 / 1.3 m ; blocs 0.4 / 0.9 / 1.6 / 2.0 m ; murs 2.6 / 3.0 / 3.5 / 3.75 / 4.25 m ; barre de slide (1.0 m) ; tunnel accroupi (1.4 m) ; couloir de wall run (murs à 4 m l'un de l'autre) |

**F6** (build debug) fait défiler les 12 points de spawn de la gym (`DevSpawns`, groupe `dev_spawn`).
