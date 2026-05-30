# Meccanica 01 — Survival a tempo decrescente

## Concept

Il timer parte da 30 secondi e scende. In pista sono sparsi dei pickup: raccoglierli **aggiunge secondi al timer**. Si vince resistendo per 90 secondi totali (cioè raccogliendo abbastanza pickup da non far mai arrivare il timer a zero); si perde quando il timer arriva a zero. È il loop "greed-vs-time": fermarsi è la morte, ma deviare per un pickup costa secondi di percorso.

## Cosa userai dal toolkit

| Componente | Dove sta | A cosa serve qui |
|---|---|---|
| [TimerManager](../Scripts/Core/TimerManager.cs) | `TimerManager` GameObject | Tempo che scende. |
| [Collectible](../Scripts/Behaviors/Collectible.cs) | Pickup "tempo" sparsi in pista | Feedback visivo/sonoro del pickup. |
| [TriggerZone](../Scripts/Behaviors/TriggerZone.cs) (effect = `BonusTime`) | Stesso GameObject del Collectible | Effetto: aggiunge secondi al timer. |
| [LoseCondition](../Scripts/Behaviors/LoseCondition.cs) (type = `TimerExpired`) | Figlio del `LevelManager` | Sconfitta se il timer arriva a 0. |
| [WinCondition](../Scripts/Behaviors/WinCondition.cs) (type = `Survive`) | Figlio del `LevelManager` | Vittoria a 90s di sopravvivenza. |
| [LevelManager](../Scripts/Core/LevelManager.cs) | `LevelManager` GameObject | Collega win e lose. |

## Setup di scena passo-passo

### Step 1 — Configura il TimerManager

Seleziona il GameObject `TimerManager` in Hierarchy. Sul componente `Timer Manager`:

| Campo Inspector | Valore |
|---|---|
| Mode | **CountDown** |
| Start Time | `30` |
| Auto Start | ✅ (vero) |
| Pause On End | ✅ (vero) |

> Perché Pause On End vero: in CountDown evita che il timer vada in negativo dopo la sconfitta. Più pulito da leggere nell'HUD.

### Step 2 — Crea il pickup "tempo"

In Hierarchy, click destro → 3D Object → Cylinder (o Sphere; è solo il visual). Rinominalo `TimePickup`.

Sul GameObject `TimePickup`:

1. Imposta il Collider esistente come **Is Trigger** ✅.
2. Aggiungi il componente `Collectible`:
   | Campo | Valore |
   |---|---|
   | Category | `time_pickup` |
   | Value | `1` |
   | Respawn After | `0` (sparisce per sempre) |
   | Float Bob | ✅ |
   | Rotate | ✅ |
   | Pickup Sfx | (un clip dai tuoi assets, opzionale) |
   | Pickup Vfx | (un prefab di particellare, opzionale) |
3. Aggiungi sul **medesimo GameObject** il componente `Trigger Zone`:
   | Campo | Valore |
   |---|---|
   | Effect | **BonusTime** |
   | One Shot | ✅ (così non si riattiva se il `Collectible` decidesse di respawnare) |
   | Cooldown | `0.5` |
   | Require Kart | ✅ |
   | Time Bonus | `5` |

> Perché due componenti sullo stesso oggetto: il `Collectible` gestisce SFX/VFX/sparizione e il conteggio per categoria, il `TriggerZone` applica l'effetto `BonusTime` al `TimerManager`. Si attivano entrambi al passaggio del kart perché tutti e due ascoltano `OnTriggerEnter` sullo stesso Collider.

4. Trasforma il `TimePickup` in **Prefab**: trascinalo dalla Hierarchy alla Project window in `Assets/_Project/02.Gameplay/Prefabs/` (crea la cartella se non esiste).
5. Duplica il prefab in scena 6–8 volte lungo la pista, distanziati di ~15 secondi di percorso a velocità di crociera.

### Step 3 — Crea WinCondition e LoseCondition

In Hierarchy crea due GameObject vuoti **come figli del `LevelManager`**:

**`Win — Survive 90s`** con componente `Win Condition`:

| Campo | Valore |
|---|---|
| Type | **Survive** |
| Survival Time | `90` |

**`Lose — Out of Time`** con componente `Lose Condition`:

| Campo | Valore |
|---|---|
| Type | **TimerExpired** |

### Step 4 — Cablali nel LevelManager

Sul GameObject `LevelManager`:

| Campo | Valore |
|---|---|
| Level Name | `Survival Run` |
| Win Conditions | (1 elemento) → trascina `Win — Survive 90s` |
| Lose Conditions | (1 elemento) → trascina `Lose — Out of Time` |
| Win Overlay | (pannello UI "You Win" se ne hai uno, opzionale) |
| Lose Overlay | (pannello UI "Game Over" se ne hai uno, opzionale) |
| Player Start | (Transform dove spawnare il kart, opzionale) |

## Collegamenti UnityEvent

**Nessuno** è obbligatorio: l'HUD aggiorna il timer da solo perché si auto-iscrive a `TimerManager.OnTimeChanged` in [HUDController.cs](../Scripts/UI/HUDController.cs), e il `Trigger Zone BonusTime` chiama `TimerManager.Instance.AddTime()` direttamente.

**Opzionali per game feel:**
- Sul `TimePickup`, evento `Collectible → On Collected (GameObject)`: trascina l'oggetto `HUDController` e seleziona `HUDController.ShowMessage` con testo `"+5s"`. Mostra un flash di testo a ogni pickup.
- Sul `TimerManager`, evento `On Time Up`: collega un AudioSource che suona un "buzz" di fine tempo.

## Test rapido

1. Apri `Assets/_Project/02.Gameplay/Scenes/02_Gameplay.unity` e premi Play.
2. L'HUD mostra `00:30` e scende.
3. Guida il kart su un `TimePickup`. Deve:
   - sparire (con eventuale SFX/VFX),
   - aggiungere 5 secondi al timer visualizzato sull'HUD.
4. Lascia scadere il timer senza prendere pickup: deve apparire il `Lose Overlay` (o almeno l'HUD deve fermarsi a `00:00` e i controlli del kart non devono più cambiare il timer).
5. Raccogli abbastanza pickup da arrivare a 90 secondi di gioco: deve apparire il `Win Overlay`.
6. **Console**: nessun warning del tipo `[TriggerZone] Manca 'TimerManager' per l'effetto BonusTime` (se compare, hai dimenticato il `TimerManager` in scena).

## Manopole da girare

| Per ottenere... | Cambia... |
|---|---|
| Più ansia | `Start Time` da 30 a 20, `Time Bonus` da 5 a 3. |
| Più rilassato (testing) | `Start Time` a 60. |
| Vittoria più lunga | `Survival Time` su `Win — Survive 90s` da 90 a 180. |
| Pickup-tempo "epico" | `Time Bonus` di un pickup speciale a 15, mettine solo 1 in pista. |
| Pickup-trappola | Aggiungi una `Trigger Zone` con `Effect = MalusTime`, `Time Malus = 5` sotto a un cespuglio: chi devia troppo finisce nella trappola. |

## Errori tipici

- **Il timer non scende.** Mode è su `CountUp`. Mettilo su `CountDown`.
- **Il pickup sparisce ma il timer non aumenta.** Non hai messo il `Trigger Zone` sullo stesso GameObject del `Collectible`, oppure `Effect` non è `BonusTime`. Controlla anche `Time Bonus > 0`.
- **Il timer aumenta di colpo di 10s passando sul pickup.** Il pickup ha sia `Collectible` che `Trigger Zone` *e* tu stai usando la variante con UnityEvent `OnCollected → AddTime`: ne stai applicando due insieme. Scegli un metodo solo.
- **Il pickup riattiva continuamente.** `One Shot` è disattivato e `Respawn After > 0` sul Collectible: dopo il respawn il trigger si riattiva. Soluzione: o `One Shot` ✅ o `Respawn After = 0`.
- **Console: `[TriggerZone] Manca 'TimerManager'`.** Hai cancellato per sbaglio il GameObject `TimerManager` dalla scena.
- **L'HUD non si aggiorna.** Verifica che il GameObject con `HUDController` sia attivo prima del `TimerManager.Start()` (di solito basta non disattivarlo).
