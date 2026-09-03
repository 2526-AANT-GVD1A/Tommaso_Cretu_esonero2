# Fix bump giunzioni — Ripristino rampe (A) + Capsula flottante (B)

Unico file toccato: `Assets/00.Project/02.Gameplay/02.Script/CarController1sos.cs`.
Scena intatta. Nessun asset nuovo.

## Contesto

- Step 1 (maxDepenetrationVelocity + attrito zero) **non** ha risolto il bump
  e **ha rotto le rampe**: l'attrito serviva ad aderire alla China ~80°, il tetto
  di depenetrazione strozzava la spinta di salita, e il gate `hasFlatGroundSupport`
  del clamp si attiva alla base della rampa (pavimento piatto entro la tolleranza
  0.15) mangiando l'impulso di salita legittimo.
- Il bump persistente è depenetrazione **posizionale** ai bordi box complanari
  (log: `Base (4)`): nessun filtro di velocità lo ferma. Serve la capsula flottante.

## A — Ripristino (rampe di nuovo funzionanti)

1. `Awake`: rimuovere l'intero blocco "Step 1" (`rb.maxDepenetrationVelocity`,
   `PhysicsMaterial` "KartNoFriction", loop assegnazione material).
2. Rimuovere il campo `kartNoFrictionMaterial` e la `Destroy` in `OnDestroy`.
3. Clamp in `UpdateVelocity`: gate da `hasFlatGroundSupport` a
   `groundHit.normal.y >= groundFlatNormalThreshold`; `expectedClimb` dalla
   normale di `groundHit`; commento aggiornato.
4. `TrySphereCastGround`: rimuovere tracciamento flat support
   (`bestFlatDistance/bestFlatCollider/bestFlatNormalY` + assegnazioni).
5. Rimuovere campi `FlatSupportTolerance`, `hasFlatGroundSupport`,
   `flatGroundSupportNormalY`, `flatGroundSupportCollider`.
6. `LogSeamHopSpike`: togliere la parte "supporto piatto" dal log (resta
   velocità, cap, hit più vicino con percorso, posizione).
7. Tooltip `seamHopMaxVerticalSpeed` e `groundFlatNormalThreshold`: tornano alla
   semantica "normale dell'hit" (senza riferimenti al supporto piatto).
8. Restano invariati: CCD `Discrete`, clamp anti-hop prima versione, diagnostica.

## B — Capsula flottante (cura radice del bump)

Nuovi campi serializzati in "Ground & Gravity":
- `groundContactClearance` (Range 0–0.15, default **0.07**): alzata del fondo
  della/e CapsuleCollider fisiche. 0 = comportamento originale.
- `minSuspensionDamping` (default **9**): tetto minimo al damping della
  sospensione, attivo solo con clearance > 0 (in scena è 0.1: senza contatto la
  molla da sola oscillerebbe a ~1.5 Hz; con 9, ζ ≈ 0.47).

Logica in `Awake` (al posto del blocco Step 1 rimosso), gated su
`groundContactClearance > 0`:
1. `suspensionDamping = Mathf.Max(suspensionDamping, minSuspensionDamping)`.
2. Per ogni `CapsuleCollider` figlia non-trigger: accorcia l'altezza lungo il
   suo asse (`direction` 0=X/1=Y/2=Z) di `clearance / lossyScale[asse]`,
   slitta il centro verso l'alto di metà dell'accorciatura (top FISSO, fondo
   sale di clearance). Skip se `axisScale <= 0.0001` o se `newHeight <
   2*radius` (capsula degenerata → si lascia com'è).
3. Effetto: su terreno piatto il kart cavalca solo sulla sospensione raycast,
   non tocca mai i bordi di giunzione → niente contatto, niente pop. Il
   contatto si ristabilisce da solo su pendenze (>~6°), atterraggi e lati
   (muri/pareti skate: contatto laterale intatto).

## Verifica

- Compile batch-mode Unity 6000.0.70f1 (exit 0).
- Playtest: rampe skate OK di nuovo; bump sparito su `Base (4)`/giunzioni piatte
  a velocità alta; drift/atterraggi ok.
- Effetto collaterale accettato: su piatto il kart si assesta ~5 cm più in basso
  (equilibrio molla compression = g/k con rideHeight 0.4). Se disturba, si alza
  `Ride Height` in Inspector sul Kart1.0.
