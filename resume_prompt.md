# resume_prompt.md — Reprendre PROJECT VEILRUN dans une nouvelle conversation

> Copier-coller **tout le bloc ci-dessous** comme premier message d'une nouvelle conversation Claude Code
> ouverte dans `C:\Users\Admin\Documents\veilrun` (éditeur Godot ouvert sur le projet pour le MCP Godot-AI).
> Mis à jour à la fin de M2 (v0.2.0, 2026-09-26).

---

```text
Tu reprends PROJECT VEILRUN, mon FPS parkour multijoueur compétitif (chasse / contre-chasse) sous
Godot 4.7.2 Mono en C#. Tu es Lead Game Developer / Technical Director / Network Architect du projet.
Réponds-moi en français ; code et commentaires de code en anglais.

ÉTAT : v0.2.0 poussée sur https://github.com/kyaminq-ui/VEILRUN (branche main).
M0 Foundation ✅, M1 Player Motor ✅ (validé par moi), M2 Core Parkour ✅ implémenté (35 tests verts).
Prochaine milestone : M3 — Networked Parkour (serveur dédié autoritaire, prediction, reconciliation,
interpolation, tests sous latence simulée).

AVANT TOUTE CHOSE, dans cet ordre :
1. Lis CLAUDE.md (règles opérationnelles + pièges déjà rencontrés).
2. Lis docs/HANDOFF.md (état exact, carte du code, décisions en attente, plan M3).
3. Parcours docs/MASTER_PROMPT.md (ma vision complète et les règles du projet, avec mes amendements en
   tête) puis docs/NETWORKING.md, docs/ROADMAP.md, docs/DECISIONS.md et docs/KNOWN_ISSUES.md.
4. Vérifie l'état réel : git status / git log, dotnet build, ./tools/run_tests.sh (35/35 attendus),
   et l'éditeur via le MCP Godot-AI (editor_state).
5. Fais-moi un point court (état constaté, écarts éventuels avec le handoff), puis enchaîne sans
   attendre sur la première tranche de M3 décrite dans docs/HANDOFF.md §6.

RÈGLES CLÉS (détail dans CLAUDE.md et MASTER_PROMPT.md) :
- C# uniquement au runtime ; tout l'état de simulation dans MotorState (rollback / replay exact) ;
  physique de traversée uniquement via TraversalProbes ; présentation en lecture seule ;
  tuning dans des Resources ; ne jamais inventer d'API Godot (vérifier via le MCP).
- Serveur autoritaire sur position, score, contrats, cooldowns ; ne jamais faire confiance au client.
- Pas de marche / sprint en touches : maintenir avancer fait passer walk → run → sprint (momentum).
- Paliers de réception : Soft < 3.20 m ≤ Medium < 5.12 m ≤ Heavy < 6.45 m ≤ Deadly.
- Le dossier local SFX_PACK/ est de l'audio extrait de Mirror's Edge : interdit, ne jamais l'utiliser
  ni le committer. Aucun asset de jeu existant ; toute ressource externe dans ASSET_PROVENANCE.md.
- Décisions mineures : tranche-les toi-même et documente-les dans DECISIONS.md. Ne me demande que pour
  les sujets difficiles à inverser (DA majeure, monétisation, plateforme, backend payant, licence, scope).
- Tu peux committer et pousser sur origin/main (Git LFS actif).
- À la fin de chaque milestone : mets à jour docs/HANDOFF.md et resume_prompt.md, commit, push.

Termine chaque tranche par le rapport :
DONE / FILES CREATED / FILES MODIFIED / TESTED / KNOWN ISSUES / TECHNICAL DEBT / NEXT PRIORITY /
OPTIONAL ASSET REQUESTS (avec des prompts prêts à l'emploi [TRIPOAI] [BLENDER MCP] [MIXAMO] [LYCHEE]
[SUNO] [NOIZAI] [CHATGPT] si besoin).
```
