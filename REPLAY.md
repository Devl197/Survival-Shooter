# Snimanje i reprodukcija

Pre ulaska u Play Mode, na objektu `SessionReplay` u sceni `Level_01` izabrati
`Startup Mode`:

- Normal: obicna igra, sa power-upovima, bez snimanja ili reprodukcije.
- Record: snimanje automatski pocinje pri pokretanju scene.
- Playback: fajl se ucitava i reprodukcija pocinje pri pokretanju scene.

Podesiti `Recording Duration` (podrazumevano 60 sekundi vremena igre) i
`File Name` (podrazumevano session.json). Rezim se pamti u Awake; menjanje
Inspectora tokom igre ne pokrece novu sesiju. Za novi test izaci iz Play Moda,
promeniti rezim i ponovo pokrenuti scenu. F6/F7 vise nemaju funkciju.
F8 prekida snimanje uz cuvanje ili zaustavlja reprodukciju. Po isteku trajanja
snimak se automatski cuva. Pauza zaustavlja vreme snimka; F8 radi i tokom pauze.

Fajl se cuva u `Application.persistentDataPath/Replays`; Console ispisuje punu
putanju. Isto ime zamenjuje prethodni snimak. Ucitavanje neispravnog ili
nepostojeceg fajla prijavljuje gresku i ne zapocinje reprodukciju.

## Pocetni uslovi

SessionReplay ima execution order -1000. U Awake iskljucuje PowerUpManager i
aktivne power-up objekte postavljene u sceni, u oba benchmark rezima. U Start,
posle inicijalizacije komponenti u Awake, pokrece snimanje/reprodukciju pre
standardnih gameplay Start metoda (ukljucujuci WaveManager) i prvog unosa.
Power-upovi ostaju iskljuceni do ponovnog ucitavanja scene, cak i nakon F8.
Normal rezim ih ne menja.

JSON nema oznaku verzije. Pri ucitavanju proveravaju se scena, ispravnost podataka
i oznaka iskljucenih power-upova.
Koristiti istu scenu, konfiguraciju talasa, oruzja i pocetne postavke pri snimanju
 i reprodukciji. Pokretanje iz sveze scene daje iste definisane pocetne uslove,
ali ne garantuje deterministicku simulaciju neprijatelja, fizike i borbe.

## Sta se reprodukuje

JSON sadrzi naziv scene, trajanje, vremenski oznacene polozaje i rotacije igraca
 i kamere, stanje hodanja i stvarno ispaljene hitce (vreme, pocetak zraka i smer).
Putanja se interpolira izmedju uzoraka iz LateUpdate. Ovo je reprodukcija putanje,
ne deterministicka reprodukcija input komandi.

Tokom reprodukcije rucni input i CameraFollow su blokirani, a Rigidbody igraca
je privremeno kinematic. Po zavrsetku se vracaju prethodno Rigidbody i
CameraFollow stanje i rucne kontrole. Igrac ostaje na zavrsnoj poziciji.
Hitci koriste postojece efekte, raycast i damage trenutnog oruzja, bez ponovne
provere cooldowna. Vise dospelih hitaca u jednom frejmu izvrsava sve raycaste,
ali vizuelni efekti mogu da se preklope.

Smrt igraca prekida snimanje uz cuvanje ili reprodukciju. Ishodi borbe mogu da
se razlikuju. Nema automatskih ponavljanja, resetovanja sveta usred partije niti
izvoza benchmark metrika.

## Play Mode provera

1. Pre Play Moda izabrati Record i trajanje 5 s. Po ulasku odmah kretati igraca
   i pucati. Proveriti automatsko cuvanje posle 5 s bez pritiskanja F6.
2. Izaci iz Play Moda, izabrati Playback i ponovo pokrenuti scenu. Proveriti
   automatsku reprodukciju od pocetka, putanju, kameru, animaciju i hitce.
   WASD, mis i Fire1 ne smeju menjati reprodukciju.
3. U oba rezima ostaviti sesiju duzu od spawn intervala power-upova. Proveriti
   da nema spawnovanja ni skupljanja postavljenih power-upova. Pokrenuti novu
   Normal sesiju i proveriti da se power-upovi ponovo normalno pojavljuju.
4. Proveriti pauzu i nastavak, F8 i vracanje rucnih kontrola. F6/F7 ili promena
   Startup Mode tokom igre ne smeju pokrenuti novu sesiju.
5. Probati nepostojeci fajl i pokvaren JSON: ocekuje se jasna
   greska bez blokiranja rucnih kontrola. Ponoviti sa ispravnim fajlom.
6. Proveriti snimak bez hitaca, kraci snimak prekinut sa F8 i prekid pri smrti.

Skripte su kompajlirane protiv Unity 2018.3.3f1 biblioteka. Interaktivne provere
iznad nisu izvrsene u ovoj izmeni.
