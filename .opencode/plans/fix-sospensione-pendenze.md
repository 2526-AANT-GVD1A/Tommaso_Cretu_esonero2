# Fix sospensione slope-aware: discesa "che vola" + rampa rallentata

Unico file: `Assets/00.Project/02.Gameplay/02.Script/CarController1sos.cs`.
Scena intatta. Il fix del bump (capsula flottante) resta invariato.

## Causa di entrambi i problemi

`ApplySuspension` smorza la velocita' verticale ASSOLUTA
(`dampingForce = -vy * suspensionDamping`, con damping portato a 9 dal tetto
minimo introdotto per la capsula flottante):

- **Discesa**: il damping frena la caduta -> velocita' terminale di caduta
  gravity/damping = 30/9 = 3.3 u/s. Su una discesa a 10 u/s pendenza 30 gradi
  servirebbe vy = -5.8 per seguire il terreno: irraggiungibile -> il kart
  "vola" finche' non ci si ferma (poi la molla lo assesta).
- **Salita**: appena vy > spring/damping = 3.3, totalForce <= 0 -> early-return
  -> la molla smette di assistere; il contatto mangia la velocita' -> il kart
  rallenta fino a quasi fermo (e il lancio skate parte debole).
- La molla e' solo push-up: senza contatto (discesa/clearance) nulla insegue
  il terreno.

## Fix: `ApplySuspension` slope-aware

1. **Smorzamento sulla DEVIAZIONE dalla vy che seguirebbe la pendenza**:
   - `expectedVy = -(v_planar . n_xz) / n.y` (proiezione della velocity
     planare sulla superficie sotto il kart)
   - `dampingForce = -(vy - expectedVy) * suspensionDamping`, clampata a
     +/- gravity.
   - Discesa: seguire il terreno richiede vy negativa -> il damping non frena
     piu' la caduta; se il kart "vola" (vy sopra l'attesa) viene tirato GIU'.
   - Salita: vy attesa positiva -> il damping non annulla piu' la molla.
2. **Molla bidirezionale**: `springForce = (rideHeight - d) * strength`
   anche negativa (estesa = tira verso il terreno) -> in discesa il kart
   INSEGUE il terreno invece di planare.
3. **Tetto alla forza verso il basso**: nuovo campo serializzato
   `suspensionMaxPullDown` (default 45) per non schiantare il kart dopo
   bordi/gradini.
4. **Pendenze riperte**: damping spento gradualmente quando la normale si
   abbassa (`dampScale = clamp01((n.y - 0.34) / 0.36)`) -> sulla parete skate
   (~80 gradi, n.y 0.17) il damping e' zero e la salita torna balistica-molla
   com'era prima della capsula flottante.
5. Rimozione dell'early-return `totalForce <= 0` (le forze negative ora sono
   legittime: pull-down di inseguimento).
6. `minSuspensionDamping` (9) resta: su piatto la deviazione coincide con vy
   e stabilizza la molla da sola come finora.

Equilibrio a riposo su piatto invariato (compression = gravity/strength).
skateRampLaunch e assenza di cast: early-return invariati.

## Verifica

- Compile batch-mode Unity 6000.0.70f1 (exit 0).
- Playtest: discesa incollata al terreno; rampa salibile a piena velocita' e
  skate launch intatto; a riposo su piatto nessuna oscillazione; bump
  originale ancora assente.
- Se la rampa faticasse ancora vicino al top (contatto su faccia riperta con
  fondo alzato): abbassare `Ground Contact Clearance` a ~0.04 in Inspector.
