# Dijagnostička serija za GC i Spawn/Despawn

`series.profiler-300.json` snima **Continuous 300 i Burst 300 × InstantiateDestroy i Pool × šest ponavljanja = 24 merenja**. Koristi isti replay od 30 s, initialLifetime 4 s i 120 zamena. Grafička i AI podešavanja jednaka su glavnoj seriji. Pauza je 10 s, timeout 600 s. Planiraj oko 16 minuta plus pripremu i finalizaciju i dovoljno prostora za mnogo binarnih fajlova.

Sa dva scenarija raspored rotira parove: prvo Continuous/Instantiate, Continuous/Pool, Burst/Instantiate, Burst/Pool; drugo Burst/Pool, Burst/Instantiate, Continuous/Pool, Continuous/Instantiate. Svaki scenario ima po tri ranije i tri kasnije pozicije, a svaki režim po tri početna mesta u paru. Sa samo dva scenarija nije moguće posle svakog merenja promeniti oba faktora i obuhvatiti sve četiri kombinacije: strogo smenjivanje zauvek bi ponavljalo samo dve. Raspored glavne serije sa šest scenarija ostaje isti.

## Priprema i pokretanje

1. Napravi **novi Windows Development Build** u posebnom folderu, sa ažuriranim skriptama. **Autoconnect Profiler i Script Debugging isključi.** U Profiler prozoru isključi Deep Profile; ako postoji build opcija Deep Profiling Support, isključi i nju. Snimanje se uključuje direktno iz igre; paralelno live snimanje nije podržano. Release build se odbija umesto tihog izostavljanja podataka.
2. Pored `Survival Shooter.exe` kopiraj novu `Run-BenchmarkSeries.ps1`, `series.profiler-300.json` i isti `session.json` od 30 s. PresentMon mora biti u `Tools/PresentMon/PresentMon-2.6.0-x64.exe`.
3. Iz foldera build-a pokreni PowerShell, po potrebi **Run as administrator** zbog ETW dozvola, i izvrši:

```powershell
.\Run-BenchmarkSeries.ps1 -Config .\series.profiler-300.json -ValidateOnly
.\Run-BenchmarkSeries.ps1 -Config .\series.profiler-300.json
```

`-ValidateOnly` potvrđuje plan od 24 snimanja i trajanje replay-a, ali ne potvrđuje vrstu build-a; to proverava igra. Za kratku probu u kopiji konfiguracije stavi `repetitions: 1` (četiri merenja), proveri da jedan segment može da se učita, pa za konačnu seriju vrati 6.

## Ograničenje od 300 frejmova i izlazni fajlovi

**Jedan dugačak `.raw` fajl ne rešava pregled cele sesije u Unity 2018.3 prozoru sa ograničenom istorijom.** Zato igra menja fajl posle **200 frejmova**. Segment može uključiti parcijalne granične frejmove, ali ostaje znatno kraći od istorije. Učitavaj segmente pojedinačno. [Unity 2018.3 dokumentacija](https://docs.unity3d.com/2018.3/Documentation/Manual/ProfilerWindow.html).

```text
GC-Spawn-300-30s-<vreme-ID>/
  Continuous-300/
    run-0001-profiler/
      profiler.json
      segment-0001.raw
      segment-0002.raw
      ...
    <PresentMon-vreme-ID>/
      session.json
      measured.csv
      ...
```

`profiler.json` sadrži scenario, režim, verziju Unity-ja, početak/kraj svakog segmenta u frejmovima i sekundama od početka snimanja i brojače stvaranja, uklanjanja i proširenja pool-a. Priprema pool-a je izvan snimanja, kao i završno čišćenje i PresentMon tail. `summary.csv` ima `profilerDirectory`, a PresentMon metapodaci `unityProfiler: true`.

Skripta zahteva Completed, 420 stvaranja uključujući početnih 300, 120 uklanjanja, nula proširenja pool-a i postojeće neprazne segmente sa razlikom start/end frejmova do 200. To proverava izlazne fajlove i metapodatke; stvarno čitanje binarnih frejmova potvrdi u Unity-ju na kratkoj probi.

## Pregled metrika

Otvori **Unity 2018.3.3f1 → Window → Analysis → Profiler**, zaustavi live Record i preko **Load** učitaj jedan `segment-XXXX.raw`. **Ne koristi Shift+Load za spajanje cele sesije**, jer bi ponovo prešla istoriju. Na probi proveri da segmenti sadrže frejmove i da ih je manje od kapaciteta istorije.

| Pitanje | Šta gledati u CPU Usage / Hierarchy |
| --- | --- |
| Koliko managed memorije se alocira? | Kolonu **GC Alloc**, `GC.Alloc` i alokacije unutar Spawn/Despawn putanja. Bajtovi nisu GC trajanje niti sva native memorija. |
| Koliko traje prikupljanje? | CPU uzorke **`GC.Collect`**. Ako nema ciklusa, zabeleži to; broj `GC.Alloc` uzoraka nije broj GC ciklusa. |
| Koliko koštaju zamene? | `Benchmark.ReplaceEnemy`, `Enemy.Spawner.Spawn`, `Enemy.Spawner.Despawn`, `Enemy.Spawner.Create`, `Enemy.Spawner.DestroyRequest`. |
| Gde se troši vreme pri aktiviranju? | Postojeće markere za reset animacije, navigacije, fizike i efekata ispod Spawn/Despawn uzoraka. |

DestroyRequest meri zahtev za uništavanje; odloženi engine rad može nastupiti kasnije i izvan tog uzorka. Ne sabiraj roditeljske i ugnježdene markere kao nezavisne troškove.

Promena i upis Profiler fajla imaju sopstveni trošak. Imena, objekti segmenata i kapacitet liste pripremaju se unapred radi smanjenja managed alokacija samog snimača; native i disk trošak ostaju. Granični frejmovi mogu sadržati promenu fajla i biti parcijalni: pregledaj i susedni segment i ne sabiraj ih naslepo. Ova serija **snima uzorke**, ali ne izvozi automatski zbir GC.Alloc bajtova ili GC milisekundi; za automatski zbir potreban je poseban analizator segmenata.

## Prozori prema prethodnim PresentMon rezultatima

Prethodni snimci pomažu da izabereš vremenski prozor, ali ne garantuju identičan frejm u novom pokretanju: Profiler menja opterećenje, a AI nije deterministički. Koristi vremena, ne stare brojeve frejmova:

- **0–1 s:** početni skokovi. Najveći početni trošak u prethodnim serijama bio je u prvih 0,2 s, pre zamena.
- **Oko 4 s:** početak prvih smrti, odvojeno od zamene.
- **Oko 6, 8, 10…28 s za Burst:** grupne zamene. Pregledaj i oko 0,5 s pre i posle, preko susednih segmenata ako treba.
- **Od 6 s, na svakih 0,2 s za Continuous:** pojedinačne zamene.

Prema `startSeconds`/`endSeconds` u `profiler.json` izaberi odgovarajući segment. Vremena su od početka Profiler snimanja neposredno pre replay-a; tačan događaj identifikuj preko `Benchmark.ReplaceEnemy`. PresentMon start-to-start interval i Unity CPU uzorci nisu ista metrika.

GC može nastupiti kasnije od alokacije koja mu je doprinela, zato se čuva cela sesija, ne samo očekivani skokovi. Ne pozivaj ručni `GC.Collect()` u testu. Ako za 30 s nema GC ciklusa, snimak ipak pokazuje alokacije; za dugoročan efekat potreban je zaseban duži eksperiment.

Development/Profiler rezultate poredi unutar ove dijagnostičke serije. Za konačna vremena frejmova zadrži glavnu Release/PresentMon seriju, jer instrumentacija i zapisivanje menjaju performanse.

## Provere implementacije

`tests/Test-Series.ps1` proverava plan od 24 merenja, prosleđivanje Profiler argumenata, putanje, metapodatke, odbijanje nedostajućih/praznih/prevelikih segmenata i pogrešnih brojača. Poseban test uz zamene Unity API-ja proverava rotaciju 200+200+200+1 frejmova, zadržavanje poslednjeg dela, vraćanje stanja i odbijanje Release build-a. Ti testovi ne generišu stvarne Unity `.raw` podatke; njihovo čitanje proverava se u stvarnom Development build-u.
