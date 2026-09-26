# VEILRUN — Performance

Cible PC initiale : **1080p · 60 FPS · PC gamer milieu de gamme**.

## 1. Budgets (à affiner et mesurer à chaque milestone)
| Poste | Budget initial | Mesuré (M1, gym, 1 runner) |
|---|---|---|
| Frame CPU totale | ≤ 16.6 ms | ~7 ms (144 FPS, vsync) sur la machine de dev |
| Physique (tick 60 Hz) | ≤ 2 ms avec 8 runners | 0.29–0.39 ms (1 runner + CSG) |
| Motor par runner et par tick | ≤ 0.05 ms | < 0.3 ms total (inclut Jolt) — à profiler finement en M3 |
| Queries physiques par runner et par tick | M1 : 1 `MoveAndSlide` ; M2 : ≤ 6 shape casts centralisés | 1 |
| Draw calls | ≤ 2 000 | non mesuré |
| Triangles visibles | ≤ 3 M | non mesuré |
| VRAM | ≤ 3 Go | non mesuré |
| Bande passante / client | ≤ 64 kbit/s montant, ≤ 256 kbit/s descendant | M3 |

Machine de dev : Windows 11 Pro, D3D12. GPU et CPU à documenter.

## 2. Règles
- Probes de parkour **centralisées** (un passage par tick), résultats réutilisés tant qu'ils sont valides.
- Pas d'allocation par tick dans le motor : `MotorState` et `InputCommand` sont des structs, `LocomotionMath` est sans allocation. Le Dev HUD construit son texte à 10 Hz.
- Pooling prévu : VFX, émetteurs audio, indicateurs temporaires, leurres.
- CSG réservé au greybox ; les maps compétitives passent en meshes statiques avec collisions simples.

## 3. Mesure
- Dev HUD (F1) : FPS, temps de process, temps de physique.
- `editor_manage monitors_get` (MCP) pendant un run.
- Profiler Godot : à utiliser à chaque milestone et à consigner ici.
