# VEILRUN — Mode CONTRACT HUNT (nom provisoire)

État : conception. Implémentation prévue en **M4** (prototype) et **M5** (chase & stealth).

## 1. Paramètres
| Paramètre | Valeur initiale | Notes |
|---|---|---|
| Joueurs | 6–8 | architecture visant 10–12 plus tard, après mesure |
| Durée | 10–15 min | à régler en playtest |
| Contrats actifs | 1 par joueur | |
| Poursuivants | ≥ 1 par joueur | |
| Respawn | contrôlé (spawn scoring) | |

Tout est serveur-autoritaire : contrats, cibles, score, respawn, validation des interceptions.

## 2. Graphe de contrats
- Permutation **sans point fixe** et **sans 2-cycle** (A→B et B→A interdits sauf s'il reste ≤ 3 joueurs).
- Préférence pour un anneau unique (A→D→F→C→B→E→A) ; réassignation locale après chaque interception pour ne pas bouleverser tout le graphe.
- Aucune période sans cible : réassignation dans le même tick serveur. Joueur en respawn → ses chasseurs sont redirigés temporairement.
- Tests unitaires (M4) : propriétés de permutation, absence de 2-cycles, stabilité sur 1 000 réassignations aléatoires, arrivées et départs de joueurs.

## 3. États d'une paire chasseur → cible
`HUNTING` (recherche) → `IDENTIFYING` (attention maintenue) → `CONFIRMED` → `ENGAGED` (tentative) → issue : `INTERCEPTED` | `CHASE` (repéré ou tentative ratée) → `ESCAPED` | `INTERCEPTED` | `COUNTERED`.

## 4. Scoring (brouillon — à équilibrer)
```
Interception = Base
             + Approach (non détecté, angle arrière, depuis la hauteur)
             + Flow (FlowLevel à l'impact, chaîne de parkour)
             + Stealth (temps sans déclencher la menace)
             + Risk (poursuivant proche, poursuite longue)
Évasion      = bonus fixe + durée de poursuite
Contre       = petit bonus + fenêtre d'évasion
Pénalités    = mauvaise cible, interception ratée
```
Affichage détaillé 2–3 s après l'action, simple et lisible.

## 5. Localisation & menace
- Signal cible : district + distance approximative par paliers + pulsation intermittente + dernière zone connue. Jamais de position exacte permanente.
- Menace (cible) : 4 niveaux aux noms originaux (à définir avec la direction artistique) calculés à partir de la distance du poursuivant, de la ligne de vue, de la durée à proximité et des actions agressives. Jamais la direction exacte.

## 6. Spawn scoring
Candidats notés selon : distance aux joueurs, ligne de vue, distance au poursuivant, distance à la cible, densité, danger, morts récentes. Tirage aléatoire parmi les meilleurs candidats. Test : aucun spawn en ligne de vue de son poursuivant ou de sa cible.

## 7. Flux de match
BOOT → MAIN MENU → LOBBY → LOADING → WARMUP → MATCH START → ACTIVE → MATCH END → RESULTS → LOBBY (transitions décidées par le serveur).
