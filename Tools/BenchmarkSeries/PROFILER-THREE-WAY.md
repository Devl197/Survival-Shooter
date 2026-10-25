# Profiler: Legacy, Fresh i Pool

Konfiguracija `series.profiler-three-way-300.json` priprema 18 snimanja:
Continuous/Burst sa 300 neprijatelja × tri varijante × tri ponavljanja.
Svako snimanje koristi postojeći replay od tačno 30 s i 120 zamena.
Redosled varijanti rotira između ponavljanja. Svaka varijanta zauzima
prvo, drugo i treće mesto unutar scenarija po jednom.

## Pokretanje

1. U Unity 2018.3.3f1 napravi nov Windows **Development Build** iz trenutnog
   projekta u `E:/Master/Unity/SurvivalBuild/Survival Shooter.exe`.
   Isključi Autoconnect Profiler, Script Debugging i Deep Profile
   (i Deep Profiling Support ako je dostupan). BenchmarkMenu treba da bude
   prva uključena scena, a Level_01 takođe uključena. Display Resolution
   Dialog treba da bude Disabled.
2. Zatvori pokrenutu igru i zaustavi Play mode i live Profiler snimanje.
3. U PowerShell-u pokreni proveru, zatim seriju:

```powershell
& 'E:\Master\Unity\Survival-Shooter\Tools\BenchmarkSeries\Run-BenchmarkSeries.ps1' -Config 'E:\Master\Unity\Survival-Shooter\Tools\BenchmarkSeries\series.profiler-three-way-300.json' -ValidateOnly
& 'E:\Master\Unity\Survival-Shooter\Tools\BenchmarkSeries\Run-BenchmarkSeries.ps1' -Config 'E:\Master\Unity\Survival-Shooter\Tools\BenchmarkSeries\series.profiler-three-way-300.json'
```

Konfiguracija već pokazuje na navedeni build i postojeći replay u
`%USERPROFILE%/AppData/LocalLow/DefaultCompany/Survival Shooter/Replays/session.json`.
Nema potrebe za kopiranjem skripte ili replay-a pored igre. Ako build praviš
na drugom mestu, promeni samo gameExecutable u novoj konfiguraciji.
ValidateOnly proverava putanje, trajanje replay-a i plan, ali ne potvrđuje
da je postojeći izvršni fajl nov Development Build.

Snimanje koristi samo Unity Profiler, 60 FPS, 1920×1080, Ultra, periodično
zadavanje odredišta na 0,2 s i CullUpdateTransforms. PresentMon se ne pokreće.
Ne koristi računar tokom serije; igra treba da ostane vidljiva i u fokusu.
Planiraj približno 13–15 minuta uključujući pauze od 10 s i učitavanje.

## Rezultati i izvoz

Svako pokretanje pravi novi folder
`E:/Master/Merenja/GC-Spawn-three-variants-300-30s-<vreme-ID>`.
U `_automation/summary.csv` proveri svih 18 statusa Completed. Konfiguracija
nastavlja posle greške, pa završetak skripte sam po sebi ne znači da su svi
snimci uspešni. Očekuje se 420 stvaranja (sa početnom populacijom), 120
uklanjanja i nula proširenja pool-a po snimku.

U Unity-ju izaberi **Tools → Benchmark → Export Profiler recordings to CSV**,
zatim folder cele nove serije i izlazni folder. Proveri status Complete za
svih 18 snimaka u Report.md. Analizirati Spawn/Despawn/Replace, alokacije
i zabeležene GC događaje; ova serija ne meri maksimalni FPS. Priprema pool-a
ostaje van snimanja. Detalji metrika: [PROFILER.md](PROFILER.md).
