# CPU i rendering posle testova senki i cullinga

`series.profiler-cpu-rendering.json` priprema šest zasebnih Unity Profiler
snimanja, bez PresentMon-a. Koristi iste kombinacije kao Release serija od
30 testova: Soft/Off senke glavnog svetla × AlwaysAnimate/CullUpdateTransforms/
CullCompletely, ali **po jedno ponavljanje** za dijagnostiku, ne statistiku FPS-a.

Sve grupe koriste `Level_01`, `BenchmarkGrouped`, 300 neprijatelja, Pool,
periodične AI zahteve na 0,5 s, offscreen isključen, motion vectors uključen,
isti replay od 30 s i 1920×1080 u prozoru, Ultra. Lighting podaci i režim
svetla se ne menjaju. Senke se menjaju samo na glavnom realtime svetlu.
Rezultati idu u novi `E:/Master/Merenja/CPU-Rendering-Grouped-300-Shadows-Culling-...`.

## Priprema bez pokretanja

Potreban je **novi Windows Development Build** u
`E:/Master/Unity/ProfilerSurvivalBuild/Survival Shooter.exe`, sa izmenama koje
beleže dijagnostičku konfiguraciju u `profiler.json`. Postojeći Release build
za PresentMon nije odgovarajući. Napravi build odvojeno od njega.

- Development Build uključen.
- Autoconnect Profiler i Script Debugging isključeni.
- Deep Profile isključen. Ako je dostupno, isključiti i Deep Profiling Support.
- Prva scena `BenchmarkMenu`, gameplay scene uključene u Build Settings.
- Display Resolution Dialog isključen; isti replay od 30 s ne prepisivati.

Provera iz korena repozitorijuma, bez pokretanja igre ili pravljenja snimaka:

```powershell
.\Tools\BenchmarkSeries\Run-BenchmarkSeries.ps1 -Config .\Tools\BenchmarkSeries\series.profiler-cpu-rendering.json -ValidateOnly
```

Očekivani plan: šest pokretanja. Provera potvrđuje ulazne putanje, trajanje
replay-a i plan; ne potvrđuje da postojeći EXE sadrži novi Development build.
Konfiguracija je pripremljena bez pokretanja snimanja. Za kasnije pokretanje
koristi se ista komanda bez `-ValidateOnly`.

## Snimanje i provere rezultata

Postojeći snimač ograničava igru na **60 FPS**, koristi bafer 128 MiB i deli
sesiju u `.raw` segmente do 200 frejmova. Priprema pool-a, početnih 300 spawnova
i završno čišćenje su izvan snimanja. Pauza između pokretanja je 10 s, timeout
600 s; prva greška prekida seriju. Planiraj najmanje oko četiri minuta ukupno,
uz vreme za učitavanje i finalizaciju.

Grouped scenario ima grupu 60, interval 1 s, prvu smrt na 5 s, death duration
2 s i seed 12345. Događaji na 28 i 29 s se izostavljaju, jer ne mogu završiti
smrt pre kraja replay-a. Očekuje se **1.380 zamena** (23 grupe × 60),
**1.680 spawnova sa početnom populacijom** i nula proširenja pool-a.
To nisu stari Continuous/Burst brojači od 120 zamena i 420 spawnova.

`profiler.json` sada ima `configuration`: scenu, scenario JSON, replay putanju,
trajanje, seed, broj neprijatelja, culling, renderer/AI opcije, stanje senki,
ime svetla i grafiku. Skripta proverava tu konfiguraciju, završne brojače,
neprazne segmente, granicu od 200 frejmova i upozorenja o izgubljenim podacima.
Snimanje duže od 32 s stvarnog vremena za ovaj replay odbija se kao zastoj.
Stariji build bez konfiguracionih metapodataka neće biti prihvaćen.

## Šta analizirati u Unity-ju

U Unity **2018.3.3f1**, Window → Analysis → Profiler, isključi live Record i
učitavaj pojedinačne `segment-XXXX.raw` fajlove pomoću Load. Otvori CPU Usage,
Timeline i Hierarchy, kao i Rendering prikaz. Proveri da svaki izabrani segment
stvarno sadrži potrebne niti, engine uzorke i rendering brojače; metapodaci sami
ne dokazuju njihov sadržaj. Ne spajaj celu sesiju u istoriju od 300 frejmova.

| Poređenje | Pitanje |
| --- | --- |
| Off + CullUpdateTransforms | Osnovni preostali trošak praktične postavke igre. |
| Soft + CullUpdateTransforms prema Off + CullUpdateTransforms | Šta nestaje na CPU/render niti i u rendering brojačima kada se isključe senke? |
| Off + AlwaysAnimate prema Off + CullUpdateTransforms | Koliko rada animacije/skinninga uklanja culling? |
| Off + CullCompletely prema Off + CullUpdateTransforms | Odakle potiče dodatni mali dobitak potpunog cullinga? |
| Soft/Off + CullCompletely i AlwaysAnimate | Kontrola da li se obrazac ponavlja kroz ostale režime. |

Na Main Thread i Render Thread prati aktivan rad, uključujući `Camera.Render`
i njegove poduzorke, animaciju/skinning, navigaciju/AI, `Physics.Simulate`,
skripte neprijatelja, GC i naše `Benchmark.ReplaceEnemy`/Spawn/Despawn markere.
Tačna imena engine uzoraka zavise od Unity-ja i rendering puta: traži stvarno
zabeležena imena. Pregledaj i worker niti u Timeline prikazu. Rendering brojači
(batches, SetPass, trouglovi/temena ako su dostupni) dopunjuju CPU vremena;
nisu sami po sebi vremena izvršavanja na GPU-u.

Razdvoji tipične frejmove bez zamene od frejmova grupne zamene i frejmova sa
periodičnim AI zahtevima. Upoređuj odgovarajuće delove replay-a, ne proizvoljan
najbrži frejm sa najsporijim. AI/fizika nisu potpuno deterministički.

**Ne pripisuj `WaitForTargetFPS`, idle i sinhronizacione wait uzorke korisnom
CPU radu.** Na 60 FPS veliki deo frejma može biti namerno čekanje. Razlikuj
ukupno i self vreme; ne sabiraj roditelje sa decom niti paralelne niti kao da
su jedan niz operacija. Granične frejmove pri rotaciji fajlova pregledaj posebno.
Development/Profiler vremena i ukupni frame time nisu direktno uporedivi sa
Release/PresentMon serijom bez FPS limita. Ova serija objašnjava raspodelu rada.

Postojeći automatski exporter je namenjen prvenstveno Spawn/Despawn/GC
markerima; njegov `Report.md` nije potpuna rang-lista CPU i rendering rada.
Za taj zaključak potreban je pregled originalnih engine uzoraka i niti u
Profiler-u. GPU trošak ne zaključuj samo iz CPU `Camera.Render` vremena.

Posle uspešnih snimaka i pregleda nalaza može se povećati `repetitions` ako
treba proveriti ponovljivost određenog markera. Za sada je pripremljeno šest
snimanja, a ne novih 30 performance testova.
