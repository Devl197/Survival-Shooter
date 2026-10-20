# Automatske serije benchmark testova

`Run-BenchmarkSeries.ps1` pokreće novu instancu Windows igre za svako merenje. Igra koristi isti benchmark, playback, pripremu i PresentMon filtriranje kao pri ručnom pokretanju kroz meni. Završava proces tek posle finalizacije snimka i čišćenja. Skripta proverava izlazni kod, izveštaj, status `Completed`, podešavanja u `session.json`, pozitivna vremena frejmova i slaganje njihovog broja sa `filteredFrames`.

## Priprema i pokretanje

1. Napravi novi **Windows standalone build** sa ovim izmenama. Prva scena treba da bude `BenchmarkMenu`, a `Level_01` i `Level_01_VisualExperiment` uključene u Build Settings (projekat je već tako podešen). U Player Settings > Resolution and Presentation > Standalone Player Options podesi **Display Resolution Dialog = Disabled** (sačuvano u projektu): igra tada ne čeka ručni izbor rezolucije i kvaliteta pre svakog testa. Ova promena zahteva novi build; skripta ne može da primeni Player Settings na stari izvršni fajl. Stari build bez podrške za automatske zahteve ostaje u meniju do isteka vremena. Za uporediva merenja koristi isti Release build, rezoluciju, kvalitet slike i ostale spoljne uslove.
2. Kopiraj `series.example.json` u svoju konfiguraciju, npr. `series.local.json`. Izmeni `gameExecutable` i `replayPath` da pokazuju na tvoj build i postojeći replay. Primer ima dve grupe, dva režima i pet ponavljanja: ukupno 20 merenja. Nazivi grupa su primeri eksperimenata koje treba da prilagodiš.
3. U PowerShell-u, iz korena projekta, proveri konfiguraciju bez pokretanja igre i pravljenja izlaznih foldera:

```powershell
.\Tools\BenchmarkSeries\Run-BenchmarkSeries.ps1 -Config .\Tools\BenchmarkSeries\series.local.json -ValidateOnly
```

4. Pokreni seriju:

```powershell
.\Tools\BenchmarkSeries\Run-BenchmarkSeries.ps1 -Config .\Tools\BenchmarkSeries\series.local.json
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

| Polje grupe | Značenje |
| --- | --- |
| `name` | Ime novog podfoldera grupe; jedinstveno unutar serije, bez separatora putanje. Naziv `_automation` je rezervisan. |
| `scene` | `Level_01` ili `Level_01_VisualExperiment`. |
| `scenario` | `BenchmarkContinuous`, `BenchmarkBurst`, `BenchmarkFrequent` ili `BenchmarkGrouped`. |
| `allocations` | Npr. `["InstantiateDestroy", "Pool"]`, ili samo jedan režim. |
| `periodicPathUpdates` | `true`: periodično ažuriranje putanja; `false`: ažuriranje svakog frejma. Ovo ne isključuje ceo AI. |
| `pathUpdateIntervalSeconds` | Interval u sekundama, najmanje `0.01`; beleži se i kada su periodična ažuriranja isključena. |
| `animatorCulling` | `AlwaysAnimate`, `CullUpdateTransforms` ili `CullCompletely`. |
| `updateWhenOffscreen` | Podešavanje skinned mesh renderer-a. |
| `skinnedMotionVectors` | Podešavanje skinned motion vectors. |

Za poređenje drugačijih scenarija, scena ili AI podešavanja dodaj novu grupu sa odgovarajućim nazivom. Grupe se izvršavaju ukrug: u prvom krugu sve grupe redom iz konfiguracije, zatim sve grupe za drugo ponavljanje i tako dalje. Dve grupe sa po jednim režimom i tri ponavljanja daju redosled `InstantiateDestroy 1, Pool 1, InstantiateDestroy 2, Pool 2, InstantiateDestroy 3, Pool 3`. Ako grupa sadrži više režima, svi njeni režimi izvršavaju se pre sledeće grupe u tom krugu. Unutar grupe prvi režim se i dalje rotira po ponavljanju: za dva režima `A,B`, zatim `B,A`, pa ponovo `A,B`. To ravnomernije raspoređuje redosled, ali ne uklanja uticaj temperature i drugih procesa. `-ValidateOnly` prikazuje plan tačno ovim redosledom.

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

```powershell
.\Tools\BenchmarkSeries\tests\Test-Series.ps1
```

Ovi testovi koriste mali zamenski proces, bez pokretanja igre i ETW snimanja. Proveravaju plan, putanje sa razmacima, odvojene nove foldere, redosled režima, timeout, neuspeh snimanja, nedostajući izveštaj, broj CSV frejmova i politiku prekidanja/nastavljanja. Testni izlazi idu u ignorisani `outputs/benchmark-series-check/`. Za potvrdu stvarnog merenja potreban je novi grafički build i serija sa ispravnim replay fajlom na lokalnoj mašini.
