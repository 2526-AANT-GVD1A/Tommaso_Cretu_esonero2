# Fix rampa: distanza perpendicolare + pull-down solo su pendenze dolci

Unico file: `Assets/00.Project/02.Gameplay/02.Script/CarController1sos.cs`.
Solo `ApplySuspension` (il resto del fix bump/discesa resta).

## Cosa si e' rotto vs commit 6bbcb1c

1. `compression = rideHeight - groundHit.distance` non clampata + pull-down
   (`-suspensionMaxPullDown`): il cast verticale su pendenza theta si gonfia
   di 1/cos(theta) (75 gradi = ~3.9x). Durante la salita dei gradi riperti
   (le rampe del livello sono gradoni a 15-75.6 gradi) il kart risulta
   "esteso" -> molla a -45 che lo tira giu' mentre sale. Pre-fix la molla era
   Clamp(...,0,rideHeight) con early-return: non tirava MAI giu'.
2. Equilibrio molla calcolato sulla distanza verticale: su gradi riperti il
   kart si assesta piu' lontano (perpendicolare) dal foglio -> cicli
   contatto/stacco; prima il contatto era costante (capsula non flottante).

## Fix (in `ApplySuspension`)

1. **Distanza perpendicolare**: `compression = rideHeight - groundHit.distance * groundHit.normal.y`
   (la distanza verticale per n.y = distanza perpendicolare al piano).
   - Azzera il gonfiaggio 1/cos(theta): su gradi riperti la molla torna al
     comportamento pre-fix (spinge su, senza risultare falsamente "estesa").
   - L'equilibrio diventa consistente a ogni angolo: su pendenza riperta il
     kart si assesta alla STESSA quota perpendicolare che su piatto ->
     rientra in contatto con il foglio come prima della capsula flottante.
   - Bonus: sulle dolci pendenze (15-25 gradi) dove il contatto non scatta
     per la clearance, la molla ora porta il kart a contatto invece di
     lasciarlo flottare.
2. **Pull-down solo dove il damping e' attivo**: `totalForce = max(totalForce,
   dampScale > 0 ? -suspensionMaxPullDown : 0)` -> su pendenze riperte
   (n.y <= 0.34, oltre ~70 gradi) la sospensione torna push-only ESATTAMENTE
   come il commit (mai forza verso il basso); su piatto e discese dolci il
   pull-down per l'inseguimento resta (fix "discesa che vola" intatto).

## Verifica

- Compile batch-mode Unity 6000.0.70f1 (exit 0).
- Playtest: salita dei gradoni 15->75 gradi a piena velocita' senza
  rallentamenti; discesa ancora incollata; a riposo nessuna oscillazione;
  bump giunzioni ancora assente; skate launch intatto.
