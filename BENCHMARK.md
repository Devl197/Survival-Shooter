# Benchmark neprijatelja

U sceni Level_01, objekat SessionReplay sada ima i BenchmarkRunner komponentu.
Benchmark je podrazumevano iskljucen (`Run Benchmark`), pa postojeca igra i
samostalni replay zadrzavaju svoje ponasanje.

## Pokretanje

1. Pre Play Moda ukljuciti Run Benchmark.
2. Dodeliti BenchmarkContinuous ili BenchmarkBurst asset polju Scenario.
3. Izabrati Allocation: InstantiateDestroy ili Pool.
4. Na SessionReplay izabrati Record za snimanje ili Playback za ucitavanje.
5. Pokrenuti Play Mode. Za drugi test izaci iz Play Moda i ponovo pokrenuti scenu.

Runner najpre priprema raspored, pool (ako je izabran) i pocetnu populaciju,
pa pokrece replay i zajednicki sat. Trajanje snimanja dolazi iz Recording Duration na SessionReplay.
Replay fajl za reprodukciju mora trajati najmanje koliko i scenario. Ako je duzi,
runner ga zaustavlja na kraju scenarija. F8 prekida ceo test. Pauza zaustavlja
zajednicko vreme. Menjati scenario i allocation samo pre Play Moda.

## Konfiguracija

- Enemy Count: stalan broj aktivnih neprijatelja.
- Group Size: broj zamena po intervalu (ne veci od Enemy Count).
- Replacement Interval: razmak izmedju pocetaka grupa.
- Initial Lifetime: vreme pocetka prve smrti; pocetna populacija postoji od vremena 0.
- Death Duration: vreme animacije smrti pre uklanjanja i zamene (podrazumevano 2 s).
- Seed: zaseban System.Random generator za ponovljive tipove i spawn tacke.
- Type Weights: relativni odnosi tipova, redosled isti kao EnemyFactory.enemyPrefab.
  Nula iskljucuje tip. Broj tezina mora odgovarati broju prefabova.

Continuous rasporedjuje Group Size zamena ravnomerno unutar intervala.
Burst izvrsava celu grupu u jednom trenutku. Slotovi se zamenjuju kruzno; isti
slot zadrzava tip, a pozicija se bira iz postojecih spawn tacaka fabrike.
Pri deljivom broju neprijatelja, kasniji ciklus (zivot i smrt zajedno) iznosi
Enemy Count / Group Size * Replacement Interval; Initial Lifetime je pocetno
odlaganje, ne nezavisni zivotni vek svakog objekta. Ako broj nije deljiv,
raspored ostaje deterministicki, a zivotni vek moze malo varirati.

Oba primera pocinju sa 50 neprijatelja, grupom 10, intervalom 2 s, prvim zamenama
na 5 s. Trajanje cele sesije odredjuje snimanje/replay. Za opterecenje menjati broj, grupu i interval.
Vise neprijatelja moze deliti iste spawn tacke i stvarati guzvu u navigaciji.
Spawn tacke moraju biti na odgovarajucem NavMesh-u.

## Kontrolisani uslovi i pooling

Redovni wave/enemy manageri, power-upovi i vec postavljeni neprijatelji se
iskljucuju za benchmark sesiju. Igrac je neranjiv; benchmark neprijatelji
primaju efekte pogotka, ali im hitci ne oduzimaju health. Samo raspored odredjuje
njihovo uklanjanje. AI, navigacija i napadi rade dok ne pocne zakazana smrt.

InstantiateDestroy pravi nov objekat za svaki spawn i deaktivira/unistava stari.
Pool unapred pravi tacno potreban broj objekata svakog tipa. Uklanjanje odmah
vraca objekat u pool; novi spawn ga aktivira i resetuje. Obe varijante pozivaju
isti reset healtha, napada, animacije, efekata, fizike i navigacije.
Obradjuju se svi dospeli dogadjaji, pa spor frejm moze objediniti vise zamena.
Ovo nije deterministicka reprodukcija fizike ili AI putanja.

## Rezultati i provera

Runner Inspector prikazuje status, broj aktivnih, spawnovanih i uklonjenih
neprijatelja, prosirenja poola i vreme pripreme u milisekundama. Console ispisuje
pocetak i kraj; nema logovanja svakog spawna. Spawned ukljucuje pocetnu populaciju,
Removed samo planirane zamene. Zavrsno ciscenje je van tih brojaca. Pool
Expansions treba da ostane 0. Priprema se meri odvojeno, a ne kao FPS metrika.

Za uporedivost pokrenuti isti scenario i replay u obe allocation varijante.
Proveriti iste zavrsne brojace, stalan Active Count i nula prosirenja poola.
Posmatrati stvaranje/uklanjanje u Hierarchy i resetovanje animacija pri zameni.
Proveriti F8, pauzu, kraj sesije i nepostojeci/neispravan replay. Na zavrsetku
objekti se ciste i vraca se prethodna neranjivost igraca; redovni manageri ostaju
iskljuceni do novog pokretanja scene.

Kompilacija protiv Unity 2018.3.3f1 biblioteka prolazi. Automatizovane provere
generatora rasporeda pokrivaju ponovljivost, redosled/vremenske granice, stabilnu
populaciju, oba obrasca, isti pocetni prefiks pri vecem broju neprijatelja i
neispravne konfiguracije. Play Mode vizuelna provera i profilisanje nisu izvrseni.
Nema CSV izvestaja, automatskih ponavljanja niti cullinga.

## Animacija smrti i status sesije

Zakazani dogadjaj prvo pokrece Dead animaciju i zvuk, zaustavlja navigaciju i
napade. Posle Death Duration objekat se uklanja/vraca u pool i stvara se zamena.
Active Count ukljucuje zive i umiruce objekte; broj zivih zato privremeno opada.
Death Duration podesiti prema animacijama, tako da bude kraci od ciklusa svakog
slota. Preduga vrednost prijavljuje gresku. Dogadjaji koji ne bi zavrsili fazu
smrti pre kraja sesije se izostavljaju. Animacioni StartSinking u benchmarku ne
unistava objekat i ne menja score; runner upravlja uklanjanjem.

UI prikazuje RECORDING/PLAYBACK, proteklo i ukupno vreme, pauzu i zavrsetak/gresku.
Osvezava se cetiri puta u sekundi u oba rezima i ima mali sopstveni trosak.
Play Mode ostaje otvoren; Profiler se ne zaustavlja niti se njegova podesavanja
menjaju. Za analizu odabrati frejmove aktivne sesije, bez pripreme i zavrsnog
ciscenja. Posle zavrsetka igre vreme i dalje tece; zavrsni UI ostaje vidljiv.

Dodatna Play Mode provera: potvrditi Dead animaciju u oba allocation rezima,
iste brojace, pravilan reset nakon povratka iz poola, zajednicki zavrsetak sa
kratkim i dugim snimkom, status nakon F8 i automatskog zavrsetka.

## Zajednicki EnemySpawner

EnemyManager i BenchmarkRunner sada koriste istu EnemySpawner komponentu na
objektu SessionReplay u Level_01. Runner vise ne cuva pool niti resetuje objekte.
Normalna igra koristi Allocation na EnemySpawner (podrazumevano Pool); benchmark
koristi svoj Allocation izbor i inicijalizuje isti spawner pre pocetka testa.
Rezim ne menjati usred sesije. WaveManager nije menjan.

Spawner ima Spawn, Despawn, Prewarm i Clear. Normalna igra prosiruje pool po
potrebi, dok benchmark unapred priprema potreban kapacitet po tipu neprijatelja.
ResetForSpawn resetuje health, smrt, tonjenje, animaciju, efekte i callback;
neranjivost je zaseban argument benchmark spawna. Resetuju se i napad, fizika i
navigacija. Ponovni Despawn istog objekta se ignorise.

U normalnoj igri smrt prijavljuje callback talasu kao ranije; StartSinking dodaje
score jednom i zakazuje uklanjanje za dve sekunde. Uklanjanje ide kroz spawner,
a pri ranijem vracanju u pool zakazani poziv se otkazuje. Neprijatelji koji nisu
stvoreni kroz spawner zadrzavaju direktno unistavanje kao rezervni put.

Play Mode provera: u Normal rezimu ubiti neprijatelje, proveriti animaciju,
tonjenje, score i zavrsetak talasa, zatim ponovno koriscenje istih objekata.
Ponoviti sa InstantiateDestroy i oba benchmark scenarija; brojaci treba da budu
isti, a pool bez prosirenja nakon benchmark pripreme. Proveriti da novi spawn
moze da prima damage u normalnoj igri i da ne cuva callback prethodnog talasa.
