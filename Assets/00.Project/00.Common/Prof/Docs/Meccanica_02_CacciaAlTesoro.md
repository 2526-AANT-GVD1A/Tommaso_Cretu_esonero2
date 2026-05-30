# Meccanica 02 — Caccia al tesoro a chiavi

## Concept

La pista è divisa in **tre segmenti** separati da muri invalicabili. In ogni segmento c'è una **chiave**: raccoglierla fa sparire il muro che dà accesso al segmento successivo. Si vince raggiungendo il tesoro nell'ultima area. È un loop di esplorazione gated: il giocatore non corre per il tempo, corre per **aprire la strada**.

La lezione tecnica chiave di questa meccanica è una: gli **UnityEvent dell'Inspector** permettono di chiamare metodi pubblici (anche `GameObject.SetActive`) senza scrivere codice. Una volta capito quello, mezzo toolkit si sblocca.

## Cosa userai dal toolkit

| Componente | Dove sta | A cosa serve qui |
|---|---|---|
| [Collectible](../Scripts/Behaviors/Collectible.cs) (category = `key`) | 3 chiavi sparse | Pickup raccoglibili. |
| Muri (GameObject normali con MeshRenderer + Collider non-trigger) | Tra un segmento e l'altro | Bloccano la strada finché attivi. |
| **UnityEvent `On Collected` del Collectible** | Sul GameObject di ogni chiave | Chiama `GameObject.SetActive(false)` sul muro corrispondente. |
| [WinCondition](../Scripts/Behaviors/WinCondition.cs) (type = `ReachPoint`) | Figlio del `LevelManager` | Vittoria al tesoro finale. |
| [LoseCondition](../Scripts/Behaviors/LoseCondition.cs) (type = `FellOffWorld`) | Figlio del `LevelManager` | Sconfitta se cadi giù dalla pista. |
| [Checkpoint](../Scripts/Behaviors/Checkpoint.cs) (opzionale) | Inizio di ogni segmento | Respawn dopo caduta. |

Niente timer in questa meccanica: il `TimerManager` può restare in scena ma con `Auto Start` ✅ in modalità **CountUp** (cronometro che misura il tempo speso, mostrato sull'HUD).

## Setup di scena passo-passo

### Step 1 — Disegna i 3 segmenti

Nella pista identifica (o costruisci) tre aree distinte. Tra una e l'altra metti un muro:

```
Pista
├── Segmento_01 ────────[ Gate_A ]────── Segmento_02 ────────[ Gate_B ]────── Segmento_03
   (start, Key_A)                       (Key_B)                              (Key_C + Tesoro)
```

I "muri" sono semplici Cube allungati, larghi a sufficienza da non poter essere aggirati. Devono avere un **Collider non-trigger** (Is Trigger ❌) così bloccano fisicamente il kart. Rinominali `Gate_A`, `Gate_B`, `Gate_C`.

> Suggerimento estetico: dai ai muri un material a tinta forte (rosso/giallo) e affianca un cartello visibile da lontano: il giocatore deve capire al primo sguardo che lì non si passa.

### Step 2 — Crea le 3 chiavi

Crea il primo `Key_A`:

1. GameObject → 3D Object → Cube (o un modello di chiave se ne hai uno).
2. Imposta il Collider come **Is Trigger** ✅.
3. Aggiungi `Collectible`:
   | Campo | Valore |
   |---|---|
   | Category | `key` |
   | Value | `1` |
   | Respawn After | `0` |
   | Float Bob | ✅ |
   | Rotate | ✅ |
   | Rotate Speed | `120` (più veloce delle monete: deve sembrare "speciale") |
4. (Opzionale) aggiungi un `Light` figlio puntato verso l'alto, color oro: rende leggibile l'oggetto-obiettivo.

Duplicalo due volte come `Key_B` e `Key_C` e mettile rispettivamente nel segmento 2 e 3.

### Step 3 — Lega ogni chiave al suo muro

Questo è il passaggio chiave (gioco di parole non voluto).

Seleziona `Key_A` in Hierarchy. Nell'Inspector, sotto il componente `Collectible`, trova l'evento **`On Collected (GameObject)`**:

1. Premi il `+` per aggiungere uno slot.
2. Trascina `Gate_A` dalla Hierarchy nel campo **None (Object)** dello slot.
3. Nel menù a tendina del metodo (di default `No Function`) seleziona: **GameObject → SetActive(bool)**.
4. Lascia la checkbox del parametro **deselezionata** (corrisponde a `SetActive(false)`).

Ripeti per `Key_B → Gate_B` e `Key_C → Gate_C`.

> Perché funziona: il `Collectible` invoca `OnCollected` quando il kart lo tocca; l'Inspector consente di chiamare `GameObject.SetActive(false)` su qualsiasi oggetto referenziato. Nessuna riga di codice scritta.

### Step 4 — Il tesoro e la WinCondition

In `Segmento_03` posiziona un GameObject vuoto chiamato `TreasureSpot` esattamente dove vuoi che termini il livello.

Crea un GameObject figlio del `LevelManager` chiamato `Win — Reach Treasure` con `Win Condition`:

| Campo | Valore |
|---|---|
| Type | **ReachPoint** |
| Target Transform | trascina `TreasureSpot` |
| Reach Radius | `2.5` |

> Reach Radius 2.5 è un compromesso: tolleranza sufficiente per non frustrare, abbastanza precisa da richiedere un arrivo intenzionale.

(Variante: puoi anche scegliere `Type = CollectN` con `Required Category = "key"` e `Required Count = 3` se preferisci "vinci appena hai tutte le chiavi", senza tesoro fisico finale. Più rapida ma meno espressiva sul piano del level design.)

### Step 5 — Sconfitta opzionale: caduta dalla pista

Crea un altro figlio del `LevelManager`: `Lose — Off Track` con `Lose Condition`:

| Campo | Valore |
|---|---|
| Type | **FellOffWorld** |
| Fall Y | `-20` |

### Step 6 — Cablali nel LevelManager

Sul `LevelManager`:

| Campo | Valore |
|---|---|
| Level Name | `Caccia al tesoro` |
| Win Conditions | trascina `Win — Reach Treasure` |
| Lose Conditions | trascina `Lose — Off Track` |
| Player Start | (un Transform all'inizio del segmento 01) |

### Step 7 — (Opzionale) Checkpoint per il respawn

Se attivi la `Lose — Off Track`, conviene dare anche un respawn umano. In ogni segmento, all'inizio, crea un GameObject `Checkpoint_Sx` con:

- Collider con **Is Trigger** ✅
- Componente `Checkpoint`:
  | Campo | Valore |
  |---|---|
  | Order | `1`, `2`, `3` per ciascuno |
  | Respawn Point | un Transform figlio chiamato `RespawnHere`, posizionato leggermente sopra il terreno |
  | One Time Only | ❌ (vogliamo poterlo riattraversare) |

> Nota: il sistema di respawn automatico dopo caduta non è già cablato; il `Checkpoint` registra il "ultimo passato" via evento. Per ora considera questo step come "metto i Checkpoint in scena, li userò in una variante più avanzata".

## Collegamenti UnityEvent

Il cuore della meccanica è il collegamento `Key_X.OnCollected → Gate_X.SetActive(false)`. Già fatto allo Step 3.

**Opzionali per game feel:**
- Su ogni `Key_X`, aggiungi un secondo slot dell'evento `On Collected` che chiama `HUDController.ShowMessage` con testo `"Chiave trovata!"`.
- Sul `Gate_X`, aggiungi un componente `Animator` con un'animazione di crollo invece di farlo sparire bruscamente: invece di `SetActive(false)` collega un `Trigger` dell'Animator.

## Test rapido

1. Premi Play. Devi essere nel `Segmento_01`, gli altri due chiusi dai muri.
2. Cerca di passare per `Gate_A`: il kart deve sbattere e rimanere indietro.
3. Raccogli `Key_A`. `Gate_A` deve sparire istantaneamente.
4. Passa al `Segmento_02`. `Gate_B` deve essere ancora chiuso.
5. Raccogli `Key_B`. Sparisce `Gate_B`.
6. Raggiungi il `TreasureSpot` nell'ultimo segmento: deve apparire il `Win Overlay`.
7. (Test caduta) Spingi il kart fuori pista: a `y < -20` deve apparire il `Lose Overlay`.
8. **Console**: nessun warning. Se compare `[TriggerZone] ...` significa che hai aggiunto un `Trigger Zone` su una chiave senza compilarne i campi — qui non serve, le chiavi sono `Collectible` puri.

## Manopole da girare

| Per ottenere... | Cambia... |
|---|---|
| Più tensione di percorso | Aggiungi sui muri delle `Trigger Zone` `Damage` o `Slow` ai loro lati, così sbatterci costa qualcosa. |
| Chiavi "fake" | Aggiungi una 4ª chiave inutile (`category = "key_fake"` invece di `"key"`). Disorienta il giocatore. |
| Meta-progressione | Imposta `Next Scene Name` del LevelManager → secondo livello dove al posto di 3 chiavi ce ne sono 5. |
| Loop con tempo | Cambia la `Win Condition` da `ReachPoint` a *due* condizioni: `ReachPoint` AND `Survive` (60s). Devi prendere le chiavi **e** essere veloce. |

## Errori tipici

- **Le chiavi non aprono i muri.** Hai linkato l'evento sul Collectible sbagliato, o hai scelto `gameObject.SetActive` sul *Collectible stesso* invece che sul muro. Ricontrolla che il target dello slot UnityEvent sia `Gate_X`.
- **Il muro non blocca il kart.** Il Collider del muro è impostato come Is Trigger ✅. Disattivalo: il muro deve essere un ostacolo *fisico*.
- **`SetActive` non compare nel menù a tendina del UnityEvent.** Hai trascinato un componente, non il GameObject. Trascina dalla Hierarchy l'**oggetto intero**, poi scegli `GameObject → SetActive(bool)` (non `Transform.SetActive`).
- **La WinCondition non scatta sul tesoro.** Hai dimenticato di assegnare `Target Transform`. Oppure `Reach Radius` è troppo piccolo: 0.5 è quasi impossibile a velocità arcade — sali a 2–3.
- **Il livello si vince subito a inizio Play.** Probabilmente il `TreasureSpot` è alla stessa posizione del `Player Start`. Spostalo.
- **Console: `[LoseCondition] HitDeadlyZone senza TriggerZone assegnato`.** Hai una `Lose Condition` di tipo sbagliato in scena. In questa meccanica usa solo `TimerExpired` o `FellOffWorld`.
