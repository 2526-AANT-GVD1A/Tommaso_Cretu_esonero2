# Guide alle meccaniche — Esonero 2

Tre tutorial **senza scrivere codice** che mostrano come comporre il toolkit fornito per ottenere tre meccaniche diverse. Servono come riferimento concreto prima di inventare la vostra: leggetene almeno una prima di mettere mano alla scena, poi usate le altre come "ricettario" quando vi blocate.

## Le tre guide

1. [Meccanica 01 — Survival a tempo decrescente](Meccanica_01_Survival.md)
   Il timer scende, raccogliere pickup aggiunge secondi, finire il tempo = sconfitta. **Difficoltà: facile.**

2. [Meccanica 02 — Caccia al tesoro a chiavi](Meccanica_02_CacciaAlTesoro.md)
   La pista è divisa in segmenti chiusi da muri; ogni chiave raccolta abbatte un muro. Si vince raggiungendo il tesoro finale. **Difficoltà: media.**

3. [Meccanica 03 — Risk & reward (scorciatoia)](Meccanica_03_RiskReward.md)
   Esiste una via lunga sicura e una corta pericolosa con boost, ostacoli e gemme di valore. **Difficoltà: media-alta** (più sul level design che sui componenti).

## Prerequisiti comuni (validi per tutte e tre)

Prima di seguire qualsiasi guida, assicuratevi che la scena `02_Gameplay.unity` abbia:

- Il **kart** con tag `Player`. Tutti i trigger del toolkit filtrano per questo tag: se manca, niente funziona.
- Sul kart i componenti [KartController](../Scripts/Core/KartController.cs), [KartInput](../Scripts/Core/KartInput.cs) e — per le guide 02 e 03 — [KartHealth](../Scripts/Core/KartHealth.cs).
- Il terreno della pista sul layer `ground` (layer 6), così il `KartController` rileva il suolo.
- Un GameObject `TimerManager` con il componente [TimerManager](../Scripts/Core/TimerManager.cs) (anche se la meccanica non usa il tempo, l'HUD lo cerca).
- Un GameObject `LevelManager` con il componente [LevelManager](../Scripts/Core/LevelManager.cs).
- Un Canvas con [HUDController](../Scripts/UI/HUDController.cs); si aggancia da solo agli eventi giusti, non serve cablarlo a mano.

## Convenzioni delle guide

- I **nomi dei campi** sono quelli mostrati dall'Inspector di Unity (es. "Max Health" e non `maxHealth`). Quando un campo non è ovvio rimando direttamente al sorgente, dove i `[Tooltip]` sono in italiano.
- I **valori** sono indicativi: sono punti di partenza ragionevoli per ottenere un loop arcade leggibile in 30–90 secondi. Cambiateli sulla vostra pista.
- Le **gerarchie di scena** sono in ASCII tree, non screenshot.

Se uno step di una guida non corrisponde a un campo Inspector reale, è la guida ad essere sbagliata, non il toolkit: scrivete al docente.
