# VEILRUN — Game Design Document

Version 0.1 — 2026-09-26 · IP originale · nom de code **VEILRUN**

## 1. Pitch
FPS parkour compétitif. 6 à 8 runners infiltrent un même district urbain. Chacun reçoit secrètement un **contrat** sur un autre joueur, alors qu'un autre joueur détient un contrat sur lui. Chaque joueur est à la fois **chasseur et cible** : retrouver sa cible, la confirmer visuellement, l'intercepter grâce à son momentum, puis disparaître avant que son propre poursuivant ne le trouve.

> « Je poursuis quelqu'un qui poursuit lui-même quelqu'un, alors qu'un quatrième joueur est probablement derrière moi. »

## 2. Piliers (non négociables)
| Pilier | Traduction concrète |
|---|---|
| FLUIDITY | parkour immédiatement plaisant, momentum central |
| BODY | caméra et son corporels, impact, effort |
| HUNT | tension constante chasseur / cible |
| READABILITY | ville lisible à haute vitesse |
| CHOICE | plusieurs routes valides, avec des compromis |
| MASTERY | écart énorme entre débutant et expert |
| FAIRNESS | pas de wallhack permanent, pas de kill gratuit |
| MULTIPLAYER FIRST | aucun système critique pensé solo |

Ordre d'arbitrage : contrôle joueur > lisibilité > fluidité > fairness réseau > skill expression > cohérence > réalisme.

## 3. Boucles
**Chasse** : spawn → contrat → zone probable → observer → identifier → planifier → approcher → intercepter → s'échapper → nouveau contrat.
**Survie (en parallèle)** : surveiller la menace → détecter le poursuivant → le semer ou le contrer.

## 4. Mode principal — CONTRACT HUNT
Détail : `MULTIPLAYER_MODE.md`. 6–8 joueurs, 10–15 min, FFA indirect, graphe de contrats en permutation, respawn contrôlé, score individuel qui récompense la qualité (discrétion, approche, flow, évasions) et pas seulement les éliminations.

## 5. Systèmes (vue d'ensemble)
| Système | Intention | Milestone |
|---|---|---|
| Parkour (`PARKOUR.md`) | cœur du jeu | M1–M2 |
| Contrats / cibles | serveur autoritaire, anneau / permutation | M4 |
| Localisation | district, distance approximative, signal intermittent, dernière zone connue | M4–M5 |
| Identification | attention visuelle maintenue → UNCERTAIN / POSSIBLE MATCH / CONFIRMED ; courir et le parkour agressif rendent plus identifiable | M4 |
| Menace | 4 niveaux (noms originaux à définir), sans direction exacte | M5 |
| Chase / Escape | poursuite déclarée ; évasion = LOS perdue + distance + durée | M5 |
| Interceptions | mêlée basée sur momentum, angle, surprise, timing ; score de qualité | M4–M5 |
| Contre | exige identification + timing + orientation + proximité | M5 |
| Gadgets | 2–3 prototypes (jammer, faux signal, leurre) avec cooldown et contre-jeu | M5+ |
| Couches verticales | rooftops = vitesse · zones peuplées = anonymat (NPC simples) | M5–M6 |

## 6. Hors scope initial
Open world, story, cinématiques, battle pass, boutique, crafting, grand arbre de compétences, 50 gadgets, 30 personnages, 20 maps, matchmaking complexe, photoréalisme, ray tracing avancé, destruction massive, véhicules.

## 7. Scope commercial initial réaliste
1 mode très solide · 2 à 4 maps · 6–8 joueurs · parkour complet · runners cosmétiques · quelques gadgets équilibrés · serveurs dédiés · progression légère · stats · settings · accessibilité.

## 8. Test d'expérience cible
Voir le scénario « Core Experience » du brief (rooftop → identification → poursuite → coupe par l'intérieur → interception → nouveau contrat → évasion par-dessus une rambarde). Chaque système doit renforcer ce scénario, sinon il est réévalué.
