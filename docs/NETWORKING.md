# VEILRUN — Networking

État : **pas encore de réseau** (prévu en M3). Ce document fixe l'architecture cible et ce que M0/M1 a déjà préparé.

## 1. Modèle cible

```
CLIENT ──(InputCommand @60 Hz, redondance ×3)──▶ DEDICATED SERVER (autorité)
CLIENT ◀──(snapshots @20–30 Hz, delta-compressés)── SERVER
client local  : prediction + reconciliation
clients distants : snapshot interpolation (buffer ~100 ms)
```

Le serveur est autoritaire sur : position finale, contrats, cibles, score, cooldowns, inventaire, dégâts, validation des takedowns.

## 2. Déjà en place (M0/M1)

| Brique | Où | Statut |
|---|---|---|
| Tick de simulation fixe 60 Hz | `Runner._PhysicsProcess` | ✅ |
| `InputCommand` compact (sequence, tick, move, yaw, pitch, buttons) | `Scripts/Player/InputCommand.cs` | ✅ |
| Boutons « maintenu ou pressé depuis le dernier échantillon » + fronts dérivés côté motor | `PlayerInput.Sample`, `PlayerMotor` | ✅ — robuste à la perte / duplication de paquets si les commandes sont renvoyées de façon redondante |
| État de simulation copiable | `MotorState` | ✅ |
| Rollback + replay déterministe | `CaptureState` / `RestoreState` (+ resync du flag sol, D-010) | ✅ testé : erreur de replay 0.000 m depuis 52 checkpoints (au sol et en l'air) |
| Interpolation visuelle entre ticks | `PlayerCamera` | ✅ (même schéma pour les distants) |

## 3. Plan M3

1. **Transport** : `ENetMultiplayerPeer` (API Godot haut niveau), canaux séparés : inputs *unreliable ordered*, snapshots *unreliable*, événements gameplay *reliable*. Vérifier via MCP / ClassDB les API exactes de 4.7.2 avant d'écrire le code.
2. **Serveur dédié** : même projet, lancé avec `--headless -- --server` ; boucle de simulation sans rendu. Export preset « Linux Server ».
3. **Abstraction d'entrée** : `IInputSource` → `LocalInputSource` (PlayerInput), `NetworkInputSource` (buffer serveur), `ReplayInputSource` (tests / replays). `Runner` ne dépendra plus directement de `PlayerInput`.
4. **Prediction** : le client garde un ring buffer `(InputCommand, MotorState)` de ~1 s. À réception d'un snapshot autoritaire pour le tick T : comparer, et si l'écart dépasse le seuil → `RestoreState(serveur)` puis rejouer les commandes T+1…now dans la frame physique.
5. **Lissage des corrections** : l'erreur visuelle est absorbée sur quelques frames par la caméra, la simulation reste exacte.
6. **Interpolation des distants** : buffer de snapshots, rendu à `now − interpDelay`.
7. **Lag compensation** : historique serveur (position, capsule, état) sur ≤ 200 ms pour valider les interceptions.
8. **Validation / anti-cheat par design** : clamp des axes de `Move`, check de vitesse vs tuning, rejet des séquences dupliquées ou anciennes, rate-limit des commandes, journal des anomalies.

## 4. NetworkTuning (à créer en M3)
Valeurs de départ, à mesurer : input 60 Hz · snapshots 20–30 Hz · buffer d'interpolation 80–120 ms · rewind max 200 ms · seuil de correction 2 cm.

## 5. Conditions de test obligatoires
30 / 80 / 150 / 250 ms · 1–5 % de perte · jitter. Un jeu qui ne marche qu'à 0 ms n'est pas fonctionnel.

## 6. Points d'attention connus
- Le contrat de déterminisme exclut pour l'instant les plateformes mobiles et les objets dynamiques.
- Le déterminisme est garanti **au sein d'un même binaire / plateforme**. Le serveur Linux et le client Windows peuvent diverger au niveau flottant : la reconciliation est conçue pour absorber ces écarts, qu'il faudra mesurer en M3.
