# VEILRUN — Audio Bible (provisoire)

Le son est une **composante mécanique**. État (v0.1.1) : audio de mouvement en place avec des **placeholders procéduraux**. Pas béton / métal, saut, 4 paliers de réception, respiration (calme ↔ effort, selon l'effort accumulé) et vent (selon la vitesse totale). Bus : Master → Music / SFX (→ Movement) / UI (`default_bus_layout.tres`).

## 1. Gameplay audio
À entendre : pas, vêtements, respiration, contacts des mains, impacts de vault, réceptions, contacts muraux, vent (fonction de la vitesse), runners proches, indices de poursuivant, environnement.

### Footsteps
Dépendent au minimum de : `SurfaceType` · vitesse · état de mouvement · intensité de réception.
Pilotage : **événements de simulation** (`MotorEvents.Footstep` émis par `MotorState.StrideCycle`, foulée de 0.75 m en marche à 1.9 m en sprint ; `MotorEvents.Landed`), pas l'animation. Le head bob partage la même phase (D-018).

### SurfaceType (extensible)
Concrete · Metal · MetalGrate · Glass · Gravel · Wood · Rubber (toiture) · Water (flaque). Porté par les métadonnées de collision (`TraversalSurface`, M2).

### Respiration
Plusieurs couches ou états (repos, effort, sprint prolongé, poursuite, récupération, réception dure), jamais une seule boucle.

## 2. Musique adaptative
Ambient électronique, minimal, pulsation progressive. Couches : exploration · chasse · poursuite · danger · stinger de résultat. Travailler en stems si possible. SunoAI : prototypes ; usage final **seulement** si la licence est compatible avec une exploitation commerciale (à vérifier au moment de l'achat ou de l'abonnement).

## 3. SFX
NOIZAI pour l'exploration : impacts, tissus, UI, whooshes, signaux, mouvement, ambiances mécaniques, technologie. Chaque fichier final est enregistré dans `ASSET_PROVENANCE.md`.

## 4. Technique
Pooling des émetteurs (`AudioStreamPlayer3D`), buses : Master / Music / SFX / Movement / UI / Voice, sidechain léger de la musique pendant les impacts.
