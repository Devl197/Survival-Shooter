# Automatske serije benchmark testova

`Run-BenchmarkSeries.ps1` pokreće novu instancu Windows igre za svako merenje. Igra koristi isti benchmark, playback, pripremu i PresentMon filtriranje kao pri ručnom pokretanju kroz meni. Završava proces tek posle finalizacije snimka i čišćenja. Skripta proverava izlazni kod, izveštaj, status `Completed`, podešavanja u `session.json`, pozitivna vremena frejmova i slaganje njihovog broja sa `filteredFrames`.

## Priprema i pokretanje

1. Napravi novi **Windows standalone build** sa ovim izmenama. Prva scena treba da bude `BenchmarkMenu`, a `Level_01` i `Level_01_VisualExperiment` uključene u Build Settings (projekat je već tako podešen). U Player Settings > Resolution and Presentation > Standalone Player Options podesi **Display Resolution Dialog = Disabled** (sačuvano u projektu): igra tada ne čeka ručni izbor rezolucije i kvaliteta pre svakog testa. Ova promena zahteva novi build; skripta ne može da primeni Player Settings na stari izvršni fajl. Stari build bez podrške za automatske zahteve ostaje u meniju do isteka vremena. Za uporediva merenja koristi isti Release build, rezoluciju, kvalitet slike i ostale spoljne uslove.
2. Kopiraj `series.example.json` u svoju konfiguraciju, npr. `series.local.json`. Kopiraj skriptu i konfiguraciju pored build-a. Primer očekuje `Survival Shooter.exe` u istom folderu i PresentMon u `Tools/PresentMon/`; izmeni `replayPath` da pokazuje na postojeći replay. Primer ima dve grupe, dva režima i pet ponavljanja: ukupno 20 merenja. Nazivi grupa su primeri eksperimenata koje treba da prilagodiš.
3. U PowerShell-u, iz foldera build-a, proveri konfiguraciju bez pokretanja igre i pravljenja izlaznih foldera:

```powershell
.\Run-BenchmarkSeries.ps1 -Config .\series.local.json -ValidateOnly
```

4. Pokreni seriju:

```powershell
.\Run-BenchmarkSeries.ps1 -Config .\series.local.json
```

Zahteva Windows PowerShell 5.1 ili PowerShell 7 i postojeći PresentMon 2.6.0 iz `Tools/PresentMon`. Skripta ne menja Windows bezbednosna podešavanja. Ako snimanje nema potrebne ETW dozvole, test se beleži kao neuspešan, kao i pri ručnom pokretanju.

Igra mora biti vidljiva i u fokusu tokom merenja. Ne koristi računar za druge poslove tokom serije: promena fokusa ili pauziranje poništavaju merenje. Automatizacija ne koristi headless/batchmode režim i ne simulira klikove po meniju. Podešavanja se prosleđuju direktno, bez menjanja sačuvanih izbora u meniju.

## Konfiguracija

Sva polja iz primera su obavezna. Nepoznata polja se odbijaju da slovne greške ne bi tiho promenile eksperiment. Putanje mogu biti apsolutne ili relativne **u odnosu na konfiguracioni fajl**, uz Windows promenljive poput `%USERPROFILE%`. U JSON-u koristi `/` ili duplo `\\` za razdvajanje foldera.

Grafička podešavanja su zajednička za **sve grupe i sva ponavljanja**. Dodaj ovaj objekat na glavni nivo postojećeg `series.local.json` (pored `repetitions` i `groups`):

```json
"graphics": {
  "width": 1920,
  "height": 1080,
  "fullscreen": false,
  "quality": "Ultra"
}
```

`width` i `height` su željene dimenzije slike u pikselima; `fullscreen: false` pokreće igru u prozoru, a `true` preko celog ekrana. `quality` je tačan naziv profila: `Very Low`, `Low`, `Medium`, `High`, `Very High` ili `Ultra`. Primer 1920×1080, prozor i Ultra je početno podešavanje koje treba prilagoditi monitoru i eksperimentu. Za drugu rezoluciju ili kvalitet pokreni novu seriju sa izmenjenim objektom.

Skripta prosleđuje vrednosti Unity player-u pre učitavanja scene, a igra dodatno bira zadati kvalitet pre pripreme benchmarka. Neposredno pre merenja proveravaju se stvarne dimenzije slike, fullscreen i kvalitet. Ako sistem ne može da primeni izabranu rezoluciju ili ih promeni tokom merenja, test se označava nevalidnim. U `session.json` se beleže `screenWidth`, `screenHeight`, `fullscreen` i `qualityLevel`; skripta ih proverava i prikazuje zadate vrednosti u `summary.csv`. `-ValidateOnly` prikazuje zajednička grafička podešavanja, ali bez pokretanja igre ne može potvrditi da monitor podržava traženi režim.

Za ovu proveru i nove metapodatke potreban je **novi build igre**. Launcher i dalje mora biti isključen kroz `Display Resolution Dialog = Disabled`. Benchmark kao i ranije isključuje VSync i ograničenje FPS-a tokom merenja, bez obzira na kvalitet koji je odabran.

| Polje serije | Značenje |
| --- | --- |
| `seriesName` | Prefiks novog foldera serije. Skripta dodaje UTC vreme i jedinstveni ID. |
| `resultsRoot` | Glavni izlazni folder. Primer koristi postojeći `Benchmarks` folder igre. |
| `gameExecutable` | Putanja do novog Windows build-a igre. |
| `presentMonPath` | Putanja do originalnog PresentMon 2.6.0 izvršnog fajla; proverava se SHA-256. |
| `replayPath` | Zajednički replay za sve testove. Čita se i ne prepisuje. |
| `repetitions` | Broj ponavljanja **svakog režima u svakoj grupi**. |
| `pauseSeconds` | Pauza između merenja, u sekundama. |
| `timeoutSeconds` | Ukupno ograničenje po procesu: učitavanje, priprema, replay i finalizacija. Postavi iznad trajanja replay-a sa dovoljnom rezervom. |
| `continueOnFailure` | `true`: zabeleži grešku i pređi na sledeći test; `false`: prekini seriju posle prve greške. |
| `groups` | Redom navedene grupe eksperimenata. |
| `graphics` | Zajednička rezolucija, fullscreen i naziv kvaliteta; važe za svako merenje. |
| `unityProfiler` | Opciono, podrazumevano `false`. `true` uključuje segmentisani Unity Profiler za Continuous 300/Burst 300, uz replay od 30 s i Development Build. |

| Polje grupe | Značenje |
| --- | --- |
| `name` | Ime novog podfoldera grupe; jedinstveno unutar serije, bez separatora putanje. Naziv `_automation` je rezervisan. |
| `scene` | `Level_01` ili `Level_01_VisualExperiment`. |
| `scenario` | `BenchmarkContinuous50`, `BenchmarkBurst50`, `BenchmarkContinuous100`, `BenchmarkBurst100`, `BenchmarkContinuous` (300), `BenchmarkBurst` (300), `BenchmarkFrequent` ili `BenchmarkGrouped`. |
| `allocations` | Npr. `["InstantiateDestroy", "Pool"]`, ili samo jedan režim. |
| `periodicPathUpdates` | `true`: periodično ažuriranje putanja; `false`: ažuriranje svakog frejma. Ovo ne isključuje ceo AI. |
| `pathUpdateIntervalSeconds` | Interval u sekundama, najmanje `0.01`; beleži se i kada su periodična ažuriranja isključena. |
| `animatorCulling` | `AlwaysAnimate`, `CullUpdateTransforms` ili `CullCompletely`. |
| `updateWhenOffscreen` | Podešavanje skinned mesh renderer-a. |
| `skinnedMotionVectors` | Podešavanje skinned motion vectors. |

Za poređenje drugačijih scenarija, scena ili AI podešavanja dodaj novu grupu sa odgovarajućim nazivom. Kada postoje najmanje četiri grupe i sve imaju isti niz od dva režima alokacije, skripta prepliće dve pomerene liste scenarija. Za šest grupa `A B C D E F` i režime `["InstantiateDestroy", "Pool"]`, prvo ponavljanje izgleda ovako:

| Merenje | Scenario | Režim |
| --- | --- | --- |
| 1 | A | InstantiateDestroy |
| 2 | D | Pool |
| 3 | B | InstantiateDestroy |
| 4 | E | Pool |
| 5 | C | InstantiateDestroy |
| 6 | F | Pool |
| 7 | D | InstantiateDestroy |
| 8 | A | Pool |
| 9 | E | InstantiateDestroy |
| 10 | B | Pool |
| 11 | F | InstantiateDestroy |
| 12 | C | Pool |

U sledećem ponavljanju svaka oznaka scenarija pomera se za jednu: A postaje B, B postaje C, ..., F postaje A. Oba režima koriste isti skup scenarija, ali različite početke cikličnog redosleda (Pool je pomeren za tri scenarija). Time se posle svakog merenja menjaju i scenario i režim, uključujući granice ponavljanja. Sa šest ponavljanja svaki scenario zauzima svako od 12 mesta tačno jednom; u svakom režimu zauzima svih šest pozicija jednom. Svaka kombinacija scenarija i režima meri se jednom po ponavljanju.

Režim koji je prvi u nizu `allocations` započinje svako ponavljanje. Više se ne smenjuje početni režim: sa parnim brojem merenja po ponavljanju to nije spojivo sa strogim smenjivanjem režima preko granica ponavljanja. Svaki scenario kroz rotaciju zauzima i rana i kasna mesta u oba režima i meri se šest puta po režimu.

Za manje od četiri grupe ili različite nizove `allocations`, skripta zadržava jednostavniji raspored: ciklična rotacija grupa između ponavljanja, svi režimi jedne grupe uzastopno i promena početnog režima po ponavljanju. Stroga promena oba faktora nije garantovana za te konfiguracije; sa jednom grupom nije ni moguća. Sa četiri ili više grupa koje imaju isti niz od dva režima, drugi tok je pomeren za `max(3, floor(brojGrupa / 2))` grupa. Pun ciklus pozicija po režimu traje onoliko ponavljanja koliko ima grupa.

Raspored važi i za poređenja drugih scena, grafičkih i AI podešavanja: pomeraju se cele grupe bez promene njihovih podešavanja. Rotacija uravnotežuje pozicije, ali ne uklanja uticaj temperature i drugih procesa i ne uravnotežuje sve moguće prethodne scenarije. `-ValidateOnly` prikazuje tačan plan izvršavanja.

## Upareni Continuous i Burst scenariji

Gotova konfiguracija `series.pool-30s.json` pokreće svih šest scenarija ispod u sceni `Level_01`, sa oba režima alokacije i po šest ponavljanja: **72 merenja**. Svaka grupa ima svoj folder. Koristi zajednički replay `%USERPROFILE%/AppData/LocalLow/DefaultCompany/Survival Shooter/Replays/session.json` (pri pripremi konfiguracije potvrđeno trajanje 30 s i scena `Level_01`) i `Survival Shooter.exe` u istom direktorijumu kao konfiguracija. `presentMonPath` je `Tools/PresentMon/PresentMon-2.6.0-x64.exe`, takođe relativno u odnosu na konfiguraciju. Ako ponovo snimiš taj replay, proveri da i dalje traje 30 s. Konfiguracija ne skraćuje duži replay.

Zajednička podešavanja su 1920×1080, prozor, kvalitet Ultra, periodično ažuriranje putanja na 0,2 s, `CullUpdateTransforms`, isključen `Update When Offscreen` i uključeni skinned motion vectors. Pauza između merenja je 10 s, a timeout po procesu 300 s. Scenariji se ciklično pomeraju za jednu poziciju po ponavljanju, a Pool koristi pomeraj od tri scenarija u odnosu na InstantiateDestroy. Posle svakog merenja menjaju se i scenario i režim. Svako ponavljanje počinje sa InstantiateDestroy da bi smenjivanje režima ostalo neprekinuto kroz celu seriju. Očekuj oko 48 minuta plus učitavanje i finalizaciju svih merenja (36 minuta snimanja i približno 12 minuta pauza).

Pre pokretanja napravi novi build sa ažuriranim scenarijima. Kopiraj `Run-BenchmarkSeries.ps1` i `series.pool-30s.json` direktno pored igre; PresentMon mora biti u podfolderu `Tools/PresentMon/`. Isti raspored relativnih putanja koristi i `series.example.json`. Provera konfiguracije potvrđuje putanje i plan; ne potvrđuje da postojeći build sadrži najnovije scenarije.

Iz foldera build-a prvo proveri plan, zatim pokreni seriju:

```powershell
.\Run-BenchmarkSeries.ps1 -Config .\series.pool-30s.json -ValidateOnly
.\Run-BenchmarkSeries.ps1 -Config .\series.pool-30s.json
```

| Broj neprijatelja | Continuous | Burst |
| --- | --- | --- |
| 50 | `BenchmarkContinuous50` | `BenchmarkBurst50` |
| 100 | `BenchmarkContinuous100` | `BenchmarkBurst100` |
| 300 | `BenchmarkContinuous` | `BenchmarkBurst` |

Svih šest scenarija koristi `groupSize = 10`, `replacementInterval = 2`, `initialLifetime = 4`, `deathDuration = 2`, `seed = 12345` i iste težine tipova neprijatelja. Continuous započinje smrt jednog neprijatelja na svakih 0,2 s; Burst započinje smrt njih 10 odjednom na svake 2 s. Uklanjanje i zamena slede 2 s kasnije. Oba obrasca imaju prosečno 5 zamena/s tokom punih ciklusa. Populacija uključuje objekte u fazi smrti; broj živih neprijatelja varira tokom ciklusa.

Za ovaj eksperiment koristi isti **replay trajanja 30 sekundi** u svim scenarijima i oba režima alokacije. Trajanje benchmarka dolazi iz replay fajla; scenario ga ne skraćuje automatski. Sa ovim podešavanjima oba obrasca daju **tačno 120 zamena** po scenariju, pored početnog stvaranja 50, 100 ili 300 neprijatelja. Continuous izvršava zamene od 6. do 29,8. sekunde, na svakih 0,2 s; Burst izvršava 12 grupa po 10 zamena od 6. do 28. sekunde, na svake 2 s. Prosek od 5 zamena/s odnosi se na pune cikluse zamene; preko cele sesije od 30 s, koja uključuje početni period bez zamena, prosek je 4 zamene/s.

Za druga trajanja potpuno jednak ukupan broj zamena dobija se sa replay-em od `6 + 2 * k` sekundi, gde je `k` pozitivan ceo broj; broj zamena je `10 * k`. Runner izostavlja događaje čija zamena pada na kraj replay-a ili kasnije, pa proizvoljna trajanja mogu odseći deo poslednjeg ciklusa. Koristi šest ponavljanja svakog scenarija u svakom režimu: šest scenarija puta dva režima puta šest ponavljanja daje 72 merenja, odnosno 36 minuta samog snimanja, bez pripreme i pauza. Šest ponavljanja obezbeđuje da svaki scenario zauzme svih 12 mesta jednom i svih šest pozicija u svakom režimu jednom. Nema uzastopnog ponavljanja scenarija niti režima kroz svih 72 merenja.

Za svaki par koristi isti replay, scenu, grafička i AI podešavanja, oba režima `InstantiateDestroy` i `Pool` i isti broj ponavljanja. Pool je unapred popunjen pre sesije; ovaj eksperiment poredi rad pripremljenog pool-a. Pripremu i eventualno proširivanje pool-a analiziraj zasebno. `BenchmarkFrequent` i `BenchmarkGrouped` ostaju dodatni stres testovi.

Scenariji su povezani u `BenchmarkMenu` sceni. Za korišćenje u samostalnoj igri napravi novi build.

## Novi folderi za svaku seriju i grupu

```text
Benchmarks/
  Poredjenje-alokacije-20261020-120000-<ID>/
    Original-periodic/
      <UTC-vreme-ID-merenja>/
        session.json
        measured.csv
        raw.csv
        capture.log
      <sledeće-merenje>/
    Visual-periodic/
      <merenje>/
    _automation/
      series.json
      summary.csv
      summary.json
      run-0001.request.json
      run-0001.report.json
      run-0001.player.log
      ...
```

Svako pokretanje kreira novi folder serije, čak i kada konfiguracija ostane ista. Grupe se uvek prave unutar tog novog foldera. Postojeći rezultati se ne koriste kao nastavak serije i ne prepisuju se. Postojeći PresentMon kod i dalje daje svakom merenju sopstveni jedinstveni folder. `summary.csv` povezuje grupu, režim, ponavljanje i stvarni folder snimka; čuva se posle svakog testa. Sirovi izlazi neuspešnih snimanja ostaju dostupni za dijagnostiku.

U postojećem Excel alatu izaberi folder nove serije da uvezeš sve njene grupe, ili folder jedne grupe za uži pregled. Uvoz pretražuje podfoldere i uzima samo `session.json`; `_automation` ne pravi dodatna merenja. Ručnu dodelu grupa u Excelu i dalje određuješ na listu Grupe.

Pri isteku vremena skripta zatvara samo instancu igre koju je sama pokrenula. PresentMon ima postojeće `--terminate_on_proc_exit` ponašanje. Ne prekida tuđe procese ili ETW sesije. Neuspešna serija završava greškom; pregled i prethodno sačuvani snimci ostaju dostupni. Zaustavljanje kroz `Ctrl+C` prekida seriju i zatvara trenutno pokrenutu igru. Nagli prekid PowerShell procesa ili gašenje računara može ostaviti poslednje merenje nedovršeno.

## Provera implementacije

Za dodatnu dijagnostičku seriju **Continuous 300 i Burst 300, oba režima, po šest ponavljanja (24 snimanja)** koristi `series.profiler-300.json`. Priprema build-a, komande, lokacija segmenata, pregled GC.Alloc/GC.Collect/Spawn/Despawn i rešenje ograničenja od 300 frejmova opisani su u [PROFILER.md](PROFILER.md). Glavna Release serija ostaje odvojena.

```powershell
.\Tools\BenchmarkSeries\tests\Test-Series.ps1
```

Ovi testovi koriste mali zamenski proces, bez pokretanja igre i ETW snimanja. Proveravaju plan, putanje sa razmacima, odvojene nove foldere, redosled režima, timeout, neuspeh snimanja, nedostajući izveštaj, broj CSV frejmova i politiku prekidanja/nastavljanja. Testni izlazi idu u ignorisani `outputs/benchmark-series-check/`. Za potvrdu stvarnog merenja potreban je novi grafički build i serija sa ispravnim replay fajlom na lokalnoj mašini.
