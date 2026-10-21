# Dijagnostička serija za GC i Spawn/Despawn

`series.profiler-300.json` snima **Continuous 300 i Burst 300 × InstantiateDestroy i Pool × jedno ponavljanje = četiri merenja**. Koristi isti replay od 30 s, initialLifetime 4 s i 120 zamena. Grafička i AI podešavanja jednaka su glavnoj seriji. Pauza je 10 s, timeout 600 s. Snima se samo Unity Profiler, bez PresentMon-a, na ograničenih 60 FPS i sa baferom od 128 MiB. Planiraj oko 2,5 minuta plus pripremu i finalizaciju. Ograničenje FPS-a smanjuje količinu podataka; ovo nije test maksimalnog FPS-a. Zadržava se 120 zamena po merenju.

Sa dva scenarija raspored rotira parove: prvo Continuous/Instantiate, Continuous/Pool, Burst/Instantiate, Burst/Pool; drugo Burst/Pool, Burst/Instantiate, Continuous/Pool, Continuous/Instantiate. Jedno ponavljanje služi dijagnostici. Ako kasnije izabereš šest ponavljanja, svaki scenario ima po tri ranije i tri kasnije pozicije, a svaki režim po tri početna mesta u paru. Sa samo dva scenarija nije moguće posle svakog merenja promeniti oba faktora i obuhvatiti sve četiri kombinacije: strogo smenjivanje zauvek bi ponavljalo samo dve. Raspored glavne serije sa šest scenarija ostaje isti.

## Priprema i pokretanje

1. Napravi **novi Windows Development Build** u posebnom folderu, sa ažuriranim skriptama. **Autoconnect Profiler i Script Debugging isključi.** U Profiler prozoru isključi Deep Profile; ako postoji build opcija Deep Profiling Support, isključi i nju. Snimanje se uključuje direktno iz igre; paralelno live snimanje nije podržano. Release build se odbija umesto tihog izostavljanja podataka.
2. Pored `Survival Shooter.exe` kopiraj novu `Run-BenchmarkSeries.ps1`, `series.profiler-300.json` i isti `session.json` od 30 s. Polje `presentMonPath` ostaje u konfiguraciji radi istog formata, ali se u ovoj seriji PresentMon ne pokreće niti proverava. Glavna Release serija i dalje koristi `Tools/PresentMon/PresentMon-2.6.0-x64.exe`.
3. Iz foldera build-a pokreni PowerShell i izvrši (ETW administratorske dozvole nisu potrebne za ovu Profiler-only seriju):

```powershell
.\Run-BenchmarkSeries.ps1 -Config .\series.profiler-300.json -ValidateOnly
.\Run-BenchmarkSeries.ps1 -Config .\series.profiler-300.json
```

`-ValidateOnly` potvrđuje plan od četiri snimanja i trajanje replay-a, ali ne potvrđuje vrstu build-a; to proverava igra. Podrazumevano je `repetitions: 1`. Proveri da se jedan segment uspešno učitava. Za statističko potvrđivanje nalaza kasnije možeš povećati broj ponavljanja.

## Lokacija snimanja

Gotova konfiguracija čuva sve rezultate u `E:/Master/Merenja`, uključujući velike `.raw` fajlove i player logove. Za D disk ili proizvoljnu lokaciju izmeni `resultsRoot`, na primer `"resultsRoot": "D:/BenchmarkRezultati"`. Relativna vrednost `"BenchmarkRezultati"` čuva rezultate pored konfiguracije. Skripta automatski pravi novi folder serije; promena putanje ne zahteva novi build. Kopiraj ažuriranu konfiguraciju pored igre. Postojeći snimci na C disku ostaju na staroj lokaciji. Detalji o čuvanju replay-a i brisanju starih rezultata nalaze se u [glavnom uputstvu](README.md#lokacija-rezultata-i-oslobađanje-prostora).

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
```

`profiler.json` sadrži scenario, režim, verziju Unity-ja, početak/kraj svakog segmenta u frejmovima i sekundama od početka snimanja i brojače stvaranja, uklanjanja i proširenja pool-a. Priprema pool-a je izvan snimanja, kao i završno čišćenje. Nema paralelnog ETW snimanja ni PresentMon tail-a. `summary.csv` ima `profilerDirectory`; `captureDirectory` je prazan jer nema PresentMon snimka. `profiler.json` beleži 60 FPS, bafer i da PresentMon nije korišćen. Manifest se zamenjuje atomskim upisom, a svaki native Profiler fajl se eksplicitno zatvara pre sledećeg segmenta ili izlaska iz igre.

Skripta zahteva Completed, 420 stvaranja uključujući početnih 300, 120 uklanjanja, nula proširenja pool-a i postojeće neprazne segmente sa razlikom start/end frejmova do 200. Odbija se i svako upozorenje `Skipping profile frame` / `Receiver can not keep up`, kao i sesija duža od 32 sekunde stvarnog vremena za replay od 30 s. Takav snimak nije potpun ni uporediv. To proverava izlazne fajlove i metapodatke; stvarno čitanje binarnih frejmova potvrdi u Unity-ju na kratkoj probi.

## Automatski izvoz postojećih snimaka u CSV i zbirnu tabelu

U projektu postoji Editor alat `Assets/Editor/BenchmarkProfilerExporter.cs`. **Novi build igre i ponovno snimanje nisu potrebni.** Koristi Unity **2018.3.3f1**, istu verziju kao za snimanje. Sačekaj da Unity kompajlira skriptu i zaustavi Play mode.

1. Otvori **Tools → Benchmark → Export Profiler recordings to CSV**.
2. Izaberi folder cele serije (npr. `E:/Master/Merenja/GC-Spawn-300-30s-...`) ili pojedinačni `run-XXXX-profiler` folder. Potrebni su originalni `profiler.json` i svi njegovi `.raw` segmenti.
3. Izaberi izlazni folder na E, D ili drugoj lokaciji. Alat u njemu pravi novi `ProfilerExport-...` podfolder; ne prepisuje postojeće snimke.
4. Otvori **Report.md** za zbirnu tabelu i napomene, ili **summary.csv** za poređenje u Excel-u. U Excel-u po potrebi koristi **Data → From Text/CSV**, UTF-8, separator zarez i decimalnu tačku.

Izvoz redom učitava segmente, pa cela sesija ne mora istovremeno stati u istoriju od 300 frejmova. Menja sadržaj trenutno učitanih Profiler frejmova: prethodno sačuvaj eventualno nesačuvano live snimanje. Snimanje Profiler-a je tokom izvoza isključeno i vraća se u prethodno stanje na kraju. Igra se ne pokreće. Za velike snimke sačekaj završetak; napredak i otkazivanje dostupni su između segmenata. Otkazan izvoz ostavlja delimične CSV fajlove i `INCOMPLETE.txt`; takav izvoz ne treba koristiti kao završni izveštaj.

| Fajl | Sadržaj |
| --- | --- |
| `Report.md` | Zbirna tabela za svaki snimak i upozorenja o pouzdanosti. |
| `summary.csv` | Jedan red po snimku: broj zamena, Spawn/Despawn ukupno/prosek/maksimum, zamene ukupno/maksimum, najskuplja grupa zamena u frejmu, pripisane alokacije i GC događaji. |
| `markers.csv` | Svaki naš marker zasebno: broj poziva, ukupno/prosečno/maksimalno/p95 vreme po pozivu, self vreme, alokacije i segment/frejm najskupljeg poziva. |
| `events.csv` | Pojedinačni custom, `GC.Alloc` i `GC.Collect` uzorci sa lokacijom u originalnom segmentu. |
| `frame-parts.csv` | Metapodaci frejmova glavne niti po segmentu, uz označene moguće parcijalne granične frejmove. Ovo nije izvor konačnih frame-time rezultata. |

Vremena su u **milisekundama**, alokacije u **bajtovima**. `durationMs` i zbirna vremena markera uključuju podređene pozive; `selfMs` ih isključuje. Nemoj sabirati `Benchmark.ReplaceEnemy` sa njegovim Spawn/Despawn podmarkerima. Isto važi za alokacije: jedna alokacija može biti pripisana i Spawn-u i nadređenoj zameni; ukupni bajtovi svih niti računaju se samo iz `GC.Alloc` uzoraka jednom. `GC.Alloc` vreme nije trajanje GC prikupljanja.

Početnih 300 Spawn poziva i prewarming su **pre početka snimanja**. Zato se za ovu seriju očekuje **120 Spawn, 120 Despawn i 120 Replace** uzoraka, iako manifest beleži ukupno 420 stvaranja. Alat proverava ove brojeve prema `manifest.removed`. Svi ukupni rezultati odnose se na zabeleženi gameplay, uz posebno pripisivanje alokacija granama Spawn/Despawn/Replace; ostale alokacije igre nisu automatski trošak neprijatelja. `secondsFromFirstSample` počinje od prvog snimljenog frejma i nije precizno vreme replay-a.

`Complete` znači da su prošle provere učitavanja, očekivanih markera i dostupnog player log-a. `Review` znači da treba proveriti napomene: nedostajući markeri, izgubljeni frejmovi, nedostupan log, drugi patch Editor-a ili nepoznat format veličine alokacije. `Invalid` označava neuspešno čitanje; ukupni rezultati tog snimka su prazni. Pojedinačno izdvojen profiler folder bez originalnog `_automation` log-a dobija `Review`, jer nije moguće proveriti upozorenja o izgubljenim podacima. Čak ni `Complete` nije dokaz da nije izgubljen nijedan nepovezan pozadinski uzorak.

GC zbir prikazuje samo spoljne `GC.Collect` uzorke na **Main Thread**, a ostale niti ostaju u `events.csv`. Ako ih nema, status je **NotObserved**, odnosno „nije zabeleženo“, a ne dokaz nultog budućeg GC troška. `DestroyRequest` obuhvata zahtev za uništavanje; odloženo native uništavanje može biti van markera. Ne mešaj ove rezultate sa Release/PresentMon vremenima frejmova.

Granični frejmovi mogu biti parcijalni. Izvoz uklanja ponovljene uzorke prema niti, putanji, markeru, apsolutnom vremenu zaokruženom na mikrosekundu, veličini alokacije i rednom broju pojavljivanja; za ponovljen uzorak čuva duže trajanje. Naš snimač menja segment **pre** Spawn/Despawn operacija, pa se njihovi markeri ne dele. Praznine tokom isključenog snimanja nije moguće rekonstruisati.

Za automatizovan izvoz iz komandne linije zatvori projekat u drugoj instanci Unity-ja i koristi novu izlaznu putanju koja još ne postoji:

```powershell
& 'E:\Unity\2018.3.3f1\Editor\Unity.exe' -batchmode -nographics -quit `
  -projectPath 'E:\Master\Unity\Survival-Shooter' `
  -executeMethod BenchmarkProfilerExporter.ExportBatch `
  -benchmarkProfilerInput 'E:\Master\Merenja\GC-Spawn-300-30s-<vreme-ID>' `
  -benchmarkProfilerOutput 'E:\Master\Merenja\ProfilerExport-novi' `
  -logFile 'E:\Master\Merenja\ProfilerExport-editor.log'
```

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

`tests/Test-ProfilerExporter.ps1` pokreće proveru izvoznika u izdvojenom Unity projektu pod `outputs/`, bez pokretanja igre i bez menjanja snimaka. Parametar `-UnityEditor` bira Editor 2018.3.3f1. Uz `-NativeSegment 'E:/.../segment-XXXX.raw'` proverava i stvarno native čitanje: poredi bajtove sa numeričkim Raw Hierarchy kolonama, ponavlja isti segment radi provere uklanjanja duplikata i proverava da nedostajući segment daje `Invalid` sa praznim rezultatima. Izaberi segment koji sadrži zamene da bi se proverila i njihova atribucija. Osnovni testovi pokrivaju nepoznate veličine alokacija, pripisivanje ugnježdenim markerima i p95.

`tests/Test-Series.ps1` proverava plan od četiri merenja, prosleđivanje Profiler argumenata, putanje, metapodatke, odbijanje nedostajućih/praznih/prevelikih segmenata i pogrešnih brojača. Poseban test uz zamene Unity API-ja proverava rotaciju 200+200+200+1 frejmova, zadržavanje poslednjeg dela, vraćanje stanja i odbijanje Release build-a. Ti testovi ne generišu stvarne Unity `.raw` podatke; njihovo čitanje proverava se u stvarnom Development build-u.

## Zašto je prethodna dijagnostička serija zastajkivala

Serija od 21. oktobra sa neograničenim FPS-om i paralelnim PresentMon-om stvarala je preko 3 GB Profiler podataka po merenju. Sva četiri player log-a imaju `Skipping profile frame`: deo podataka je izgubljen, uključujući tri merenja koja je stara skripta označila kao Completed. Oko 18 s izmerene su pauze od 13–15 s; replay od 30 s trajao je 38–47 s stvarnog vremena. Četvrti test dodatno je pao pri gašenju PresentMon-a, a njegov Profiler manifest je prazan.

Ti snimci mogu poslužiti za pregled pojedinačnih događaja, ali nisu potpuna osnova za zbir GC.Alloc/GC vremena ili poređenje performansi. Ograničenje istorije na 300 frejmova nije uzrok ovih pauza: problem je prevelika količina podataka i opterećenje snimanja. Novi režim smanjuje ulaznu stopu, odvaja Profiler od PresentMon-a i prijavljuje gubitak frejmova kao neuspeh. Njegov stvarni efekat treba potvrditi jednim novim četvorostrukim snimanjem; automatski testovi ne mere brzinu diska ili native Profiler-a.
