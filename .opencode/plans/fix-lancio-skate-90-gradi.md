# Diagnostica lancio skate (parete 90°) + sospensione pre-fix sui gradi riperti

Unico file: `Assets/00.Project/02.Gameplay/02.Script/CarController1sos.cs`.

## Stato

- I gradoni ora si salgono (il kart ARRIVA alla parete 90°), ma alla parete
  "ci sbatte e torna giu'" invece di comportarsi come prima.
- Chiarimento utente: PRIMA delle modifiche il kart "semplicemente non
  saliva" la parete 90°. Quindi l'obiettivo NON e' un nuovo climb skate:
  e' ripristinare ESATTAMENTE il feel pre-fix (commit 6bbcb1c) all'arrivo
  sulla parete, eliminando il peggioramento (sbattuta piu' violenta).
- Il codice di lancio (`OnCollisionStay` + ramo balistico di `UpdateVelocity`)
  e' IDENTICO al commit 6bbcb1c: non e' stato toccato.
- Causa probabile del peggioramento: la sospensione attuale sui gradi
  riperti (perpendicolare + senza damping) e' piu' forte di quella pre-fix
  (verticale clampata + damping 0.1): il kart arriva alla parete piu'
  veloce/eventualmente in aria -> sbattuta piu' dura e ricaduta peggiore.
  Inoltre il floor del damping (9) oggi sovrascrive lo 0.1 di scena anche
  dove il ramo riperto non lo vuole.
- Non posso dedurre i numeri reali (vy/orizzontale all'arrivo, se il lancio
  scatta, su che collider) senza dati di runtime: serve UNA misura.

## Modifiche

1. **Diagnostica del lancio** (gated dal gia' esistente
   `logSeamHopDiagnostics`, throttled):
   - Al PRIMO contatto riperto (>80 gradi) che fa scattare il lancio: nome
     collider + layer + angolo, `lastSetVelocity`, `rb.linearVelocity`
     attuale, `launchVelocity` catturata, `frozenPitch`, normale del muro,
     posizione.
   - All'USCITA dal lancio (rientro a terra): vy e posizione.
   - Costo: ~30 righe, zero impatto sul comportamento.

2. **Ramo sospensione pre-fix sui gradi riperti** (assicurazione, ripristina
   ESATTAMENTE il comportamento del commit sotto n.y <= 0.34, dove oggi gira
   la variante perpendicolare/deviazione):
   - In `Awake`, prima del tetto minimo: salvare il damping originale della
     scena in un campo (`suspensionDampingSenzaTetto`) — oggi il floor a 9
     sovrascrive lo 0.1 della scena e sulle pareti riperte smorzerebbe la
     salita (bug gia' visto).
   - In `ApplySuspension`, se `dampScale <= 0` (pendenza riperta): usare la
     logica pre-fix INTATTA: compression = Clamp(rideHeight - distance, 0,
     rideHeight), early-return se <= 0, damping = -vy * dampingOriginale,
     early-return se forza <= 0.
   - Il ramo dolce/piatto (dampScale > 0) resta com'e' (perpendicolare +
     deviazione + pull-down: fix discesa intatto).

## Verifica

- Compile batch-mode Unity 6000.0.70f1 (exit 0).
- Playtest: un tentativo di rampa con `Log Seam Hop Diagnostics` ATTIVO;
  incollare le righe di log del lancio -> da lì fix mirato (se il lancio
  parte debole per via della velocity orizzontale, la correzione sara'
  proiettare la velocity catturata sul piano tangente al muro preservando
  la magnitudine = conversione velocita' in scalata, stile skate).
