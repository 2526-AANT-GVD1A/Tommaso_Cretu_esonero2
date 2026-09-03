# Fix scalata parete skate: hit a distanza zero + clamp solo da kart supportato

Unico file: `Assets/00.Project/02.Gameplay/02.Script/CarController1sos.cs`.
Due modifiche chirurgiche; nessun altro comportamento toccato.

## Diagnosi dai log (debugrambabug.xml)

- La scalata della parete 90° ORA FUNZIONA: vy 10-12 m/s (pre-fix il kart
  non saliva affatto).
- Ma al bordo superiore lo SphereCast (raggio 0.35) becca la PIATTAFORMA
  piana in cima (`Rampa (2)/Plane (22)`, normal.y 1.00) con **d = 0.00**:
  la sfera parte gia' sovrapposta al piano perche' il centro del kart passa
  alla sua quota mentre i piedi sono ancora sul muro.
- Conseguenze a catena:
  1. `groundHit` = piano piatto falso -> il filtro anti ghost-bump cap-pa
     la vy di salita a 0.6 (log: vy 10.65/12.49/12.64 -> 0.60) -> caduta.
  2. `IsGrounded` falso-positivo -> il lancio esce prematuramente
     ("Skate launch END" a meta' salita).
- Il ciclo cap/END nei log e' il kart che rimonta, viene ucciso al bordo,
  ricade, e riprova.

## Fix 1 — `TrySphereCastGround`: scartare gli hit a distanza ~zero

Nel loop sugli hit, dopo i filtri esistenti, aggiungere:
`if (hit.distance <= 0.01f) continue;`
- Un hit a distanza ~0 significa "la sfera parte gia' sovrapposta al
  collider": non e' terreno sotto i piedi, e' un piano DI LATO/SOPRA il kart
  (la piattaforma in cima alla parete). Non e' supporto.
- Con questo, durante la scalata non arriva piu' nessun falso
  IsGrounded/groundHit -> il lancio prosegue fino al bordo, il kart crest-a
  la parete e atterra VERO sulla piattaforma (origin sopra il piano ->
  d ~0.12, normale piatta) -> "Skate launch END" solo dopo l'atterraggio.
- Sicurezza: se in un atterraggio durissimo l'origin sprofondasse (d ~0),
  il salto di un frame e' coperto da groundedGraceTime (0.08).

## Fix 2 — Clamp anti ghost-bump: solo da kart REALLY supportato

In `UpdateVelocity`, al gate del clamp aggiungere il requisito che la molla
sia compressa (il kart appoggiato), calcolata sulla quota perpendicolare:
`float supportCompression = rideHeight - groundHit.distance * groundHit.normal.y;`
e richiedere `supportCompression > 0f`.
- Appoggiato su piatto: d ~0.12 -> compression ~0.28 > 0 -> il clamp funziona
  come sempre sui ghost bump (log conferma: d 0.11-0.13 nei bump reali).
- In volo sopra un piano che il cast vede (arco di lancio oltre il bordo):
  d > rideHeight -> compression < 0 -> NESSUN cap: l'arco resta intatto.
  (Oggi il clamp cap-pere anche l'arco: vy > 0 + hit piatto sotto.)

## Verifica

- Compile batch-mode Unity 6000.0.70f1 (exit 0).
- Playtest rampa col log attivo: attesi "Skate launch START" e poi END con
  vy di ATTERAGGIO sulla piattaforma; niente piu' cap con d 0,00; nessun
  ciclo cap/END al bordo.
- Regressioni da controllare: bump alle giunzioni ancora assente (i bump
  reali hanno d 0.11-0.13 e compression > 0 -> il clamp continua a fare il
  suo), discese incollate, riposo stabile.
