# Meccanica 03 — Risk & reward (scorciatoia)

## Concept

La pista ha due strade verso il traguardo: una **lunga e sicura** e una **corta ma piena di pericoli**. La via lunga è priva di pericoli ma non basta da sola a vincere; la via corta è seminata di ostacoli che fanno danno (3 vite e sei morto), ma è l'unico posto dove si trovano le **gemme**, e per vincere servono 5 gemme. Il giocatore decide ogni giro quanto rischiare.

A differenza delle prime due, qui il toolkit è quasi tutto già visto: la difficoltà non sta nei componenti, sta nel **disporli in scena** in modo che il giocatore capisca la scelta al primo sguardo.

## Cosa userai dal toolkit

| Componente | Dove sta | A cosa serve qui |
|---|---|---|
| [KartHealth](../Scripts/Core/KartHealth.cs) | Sul kart | Sistema vite. |
| [TriggerZone](../Scripts/Behaviors/TriggerZone.cs) (effect = `Boost`) | All'imbocco della scorciatoia | Premia chi rischia con un mini-spunto. |
| [TriggerZone](../Scripts/Behaviors/TriggerZone.cs) (effect = `Damage`) | Sparse nella scorciatoia | Hazard che tolgono vite. |
| [MovingObstacle](../Scripts/Behaviors/MovingObstacle.cs) | 2 ostacoli mobili nella scorciatoia | Bersagli mobili (più drammatici degli statici). |
| [Collectible](../Scripts/Behaviors/Collectible.cs) (category = `gem`) | Solo nella scorciatoia | Il premio. |
| [WinCondition](../Scripts/Behaviors/WinCondition.cs) (type = `CollectN`) | Figlio del `LevelManager` | Vinci a 5 gemme. |
| [LoseCondition](../Scripts/Behaviors/LoseCondition.cs) (type = `HealthDepleted`) | Figlio del `LevelManager` | Sconfitta se finisci le vite. |

Niente timer obbligatorio (il `TimerManager` può restare in CountUp come cronometro estetico).

## Setup di scena passo-passo

### Step 0 — Disposizione della pista

Prima di toccare qualsiasi componente, decidi il **layout**. Questo è il 70% del lavoro per una meccanica di tipo risk & reward.

```
                    ┌──── via lunga (sicura, niente gemme) ──────┐
                    │                                            │
Start ──── Bivio ───┤                                            ├──── Traguardo
                    │                                            │
                    └──── scorciatoia (Boost + hazard + gemme) ──┘
```

**Regole di lettura del bivio:**
- Il bivio deve essere **dopo un rettilineo**: il giocatore ha 2–3 secondi per scegliere mentre vede entrambe le strade.
- La scorciatoia parte con una zona Boost: questo *invita* a entrare e dà subito il senso di "ricompensa".
- Le gemme devono essere visibili **dalla via lunga**: dotale di un visual alto/luminoso. Il giocatore sulla strada sicura deve *vedere* cosa sta perdendo.
- I muri/dislivelli devono impedire un giocatore "furbo" di entrare nella scorciatoia, prendere una gemma e tornare indietro: una volta dentro si finisce dentro.

### Step 1 — Configura il kart

Sul GameObject del kart (quello con tag `Player`), aggiungi (se non c'è già) `Kart Health`:

| Campo | Valore |
|---|---|
| Max Health | `3` |
| Invulnerability After Hit | `1.0` |

> Invulnerability 1s è quasi obbligatoria: senza, due `Damage` consecutivi nello stesso frame uccidono il kart prima ancora che possa reagire.

### Step 2 — Costruisci l'ingresso della scorciatoia

All'inizio della scorciatoia, GameObject vuoto chiamato `ShortcutBoost` con:

- Box Collider impostato come **Is Trigger** ✅, dimensionato a coprire l'imbocco della corsia.
- Componente `Trigger Zone`:
  | Campo | Valore |
  |---|---|
  | Effect | **Boost** |
  | One Shot | ❌ |
  | Cooldown | `2` (evita riattivazioni se passi due volte) |
  | Require Kart | ✅ |
  | Boost Magnitude | `1.8` |
  | Boost Duration | `2.5` |

### Step 3 — Sparge gli hazard statici

Crea 2–3 GameObject vuoti `Hazard_01`, `Hazard_02`... nella scorciatoia, posizionati tra una gemma e l'altra. Su ciascuno:

- Box Collider come **Is Trigger** ✅, abbastanza ampio da non essere "schivabile a occhio chiuso" ma non così largo da essere ineluttabile (lascia margine di sterzata).
- `Trigger Zone`:
  | Campo | Valore |
  |---|---|
  | Effect | **Damage** |
  | One Shot | ❌ (se ci ripassi, devi pagare di nuovo) |
  | Cooldown | `1` (uguale al tempo di invulnerabilità di KartHealth: evita doppie-attivazioni inutili) |
  | Require Kart | ✅ |
  | Damage Amount | `1` |
- Aggiungi un visual chiaramente "ostile" (cubo rosso, picchi, fuoco). Il giocatore deve riconoscere a colpo d'occhio "qui faccio male".

### Step 4 — Aggiungi 2 ostacoli mobili

Negli stretti della scorciatoia, GameObject `MovingHazard_01` con:

- Visual (es. cubo).
- Collider **Is Trigger** ✅.
- `Trigger Zone` come uno statico (Effect `Damage`, Damage Amount 1).
- Componente `Moving Obstacle`:
  | Campo | Valore |
  |---|---|
  | Mode | **Translate** |
  | Translate Speed | `4` |
  | Pause At Ends | `0.2` |
  | Loop Type | **PingPong** |
  | Waypoints | 2 Transform figli, ai due estremi dell'oscillazione (es. -2m e +2m lungo l'asse X) |

Duplica come `MovingHazard_02` con fase diversa (sposta i waypoint o cambia leggermente la velocità) così i due ostacoli non sono sincronizzati: più imprevedibile.

### Step 5 — Posiziona le gemme

Lungo la scorciatoia, distribuisci 6–8 `Gem` (più del minimo richiesto, così c'è tolleranza):

- GameObject (sfera o asset gemma).
- Collider **Is Trigger** ✅.
- `Collectible`:
  | Campo | Valore |
  |---|---|
  | Category | `gem` |
  | Value | `1` |
  | Respawn After | `0` |
  | Float Bob | ✅ |
  | Rotate | ✅ |
  | Rotate Speed | `120` |

> Niente `Trigger Zone` qui: la gemma è un puro punteggio, non un effetto.

### Step 6 — Win e Lose condition

Figli del `LevelManager`:

**`Win — 5 Gems`** con `Win Condition`:
| Campo | Valore |
|---|---|
| Type | **CollectN** |
| Required Count | `5` |
| Required Category | `gem` (deve coincidere esattamente con quella sui pickup) |

**`Lose — No Lives`** con `Lose Condition`:
| Campo | Valore |
|---|---|
| Type | **HealthDepleted** |
| Kart Health Ref | (lascia vuoto, lo cerca per tag Player) |

Cabla entrambi nelle liste del `LevelManager`.

## Collegamenti UnityEvent

Opzionali ma fortemente consigliati per il *feel*:

- **`KartHealth → On Damaged`**: collegalo a `HUDController.ShowMessage` con testo `"OUCH!"`, e a un AudioSource che suona un colpo. Senza feedback sonoro il giocatore non capisce di aver perso una vita.
- **`KartHealth → On Health Changed`**: già auto-collegato dall'HUD (mostra le vite).
- **`Collectible → On Collected`** su ogni gemma: a un AudioSource "ding". Le gemme devono dare gratificazione sonora immediata.

## Test rapido

1. Premi Play.
2. Vai sulla **via lunga**. Arrivi al traguardo *senza* aver vinto: l'HUD mostra `0/5` gemme. Il livello non si chiude.
3. Restart e prendi la **scorciatoia**:
   - All'ingresso, il `Boost` deve dare uno spunto di velocità chiaro (controlla l'effetto skidmark / suono).
   - Toccare un hazard statico: la vita scende da 3 a 2, l'HUD lampeggia / mostra `OUCH!`, e per ~1s i danni successivi vengono ignorati.
   - Le gemme spariscono al passaggio, contatore aumenta.
4. Prendi 5 gemme: appare il `Win Overlay`.
5. Toccare 3 hazard senza prendere abbastanza gemme: appare il `Lose Overlay`.
6. **Console**: nessun warning. Se compare `[TriggerZone] Manca 'KartHealth' per l'effetto Damage`, hai dimenticato `Kart Health` sul kart.

## Manopole da girare

Sono le manopole che cambiano l'**equilibrio** della scelta, e sono il cuore della valutazione di un livello risk-vs-reward:

| Per ottenere... | Cambia... |
|---|---|
| Scorciatoia più appetibile | `Boost Magnitude` da 1.8 a 2.2; metti il traguardo abbastanza vicino da rendere il guadagno percepibile. |
| Scorciatoia troppo facile? | Aggiungi hazard, riduci `Cooldown` degli hazard a 0.5, alza `Damage Amount` a 2 (così bastano 2 colpi per morire). |
| Scorciatoia troppo dura? | `Max Health` da 3 a 5; riduci a 2 hazard; allarga i corridoi tra hazard e parete. |
| Più rigiocabilità | `Required Count` da 5 a 8: forzi a fare la scorciatoia più volte (su una pista con loop). |
| Riequilibrare la via lunga | Aggiungi `Collectible` `category = "coin"` value 1 lungo la via lunga, e una **seconda** WinCondition opzionale (es. `CollectN coin 20`): chi non vuole rischiare, fa il grinding. Attenzione: cambia profondamente la meccanica, non farlo se non hai tempo di testare. |

## Errori tipici

- **Tocco un hazard e la vita crolla a 0 di colpo.** `Invulnerability After Hit` è troppo basso (es. 0.1), e/o `Damage Amount` è 3+. Riporta `Invulnerability After Hit = 1`.
- **L'HUD non mostra le vite.** Il GameObject del kart non ha tag `Player`. Lo `HUDController` cerca il `KartHealth` per tag.
- **Boost non parte.** Il Collider del `ShortcutBoost` non è `Is Trigger`. Oppure non hai messo `Kart Controller` sul kart.
- **Le gemme non contano per la vittoria.** Mismatch tra `Category` sul Collectible (`gem`) e `Required Category` sulla Win Condition (`gems`, `Gem`, `coin`...). Le maiuscole contano.
- **Ostacolo mobile fermo.** La lista `Waypoints` ha meno di 2 elementi. Aggiungine almeno 2.
- **Il giocatore non capisce dove sia la scorciatoia.** Problema di level design, non di codice. Aggiungi un cartello, una luce rossa, una palette diversa sull'asfalto. Il toolkit non lo fa per te.
- **Console: `[TriggerZone] Manca 'KartHealth' per l'effetto Damage`.** Il GameObject taggato `Player` non ha il componente `KartHealth`. Aggiungilo.
