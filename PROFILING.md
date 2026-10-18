# Dijagnostika trzaja pri grupnim zamenama

Markeri su namenjeni Unity 2018.3.3f1. Koriste `Profiler.BeginSample/EndSample`
preko strukture `EnemyProfilerSample`: sam scope ne pravi managed alokacije,
a zatvara se i pri `return` ili izuzetku. Imena su konstantni stringovi.
Unity uklanja pozive tih API-ja kada `ENABLE_PROFILER` nije definisan.
Raspored zamena, resetovanje i ponašanje neprijatelja nisu promenjeni.

## Snimanje sledećeg testa

1. Napravi novi Windows build sa **Development Build** uključenim.
   **Script Debugging** i **Deep Profiling** ostavi isključene. Za snimanje u fajl
   nije potreban **Autoconnect Profiler**; može ostati isključen.
2. Iz direktorijuma builda pokreni PowerShell komandu:

   ```powershell
   & '.\Survival Shooter.exe' --benchmark-profiler-log
   ```

   Prilagodi samo naziv exe fajla ako se razlikuje. Argument obrađuje naš kod;
   bez njega nema automatskog snimanja. Deluje samo u Development Player buildu,
   ne u Editoru niti običnom Release buildu.
3. U meniju izaberi **BenchmarkGrouped**, **Pool**, isti replay,
   uključen periodični AI i interval **0,5 s**. Zadrži rezoluciju i kvalitet.
   Za ovu dijagnostičku seriju isključi **Capture with PresentMon**, da snimanje
   CSV-a ne dodaje sopstveni trošak. Ovaj test ne ulazi u dosadašnje FPS proseke.
4. Pokreni benchmark i pusti da završi svih 30 s. Ne pauziraj tokom testa.
   Snimanje počinje posle pripreme pool-a i početne populacije. Završava se pri
   završetku/prekidu testa, pre čišćenja neprijatelja. Ako je PresentMon ipak
   uključen, log obuhvata i njegovu pripremu/završnu fazu; analiziraj aktivnu sesiju.
5. Log se automatski snima u:

   ```text
   %USERPROFILE%\AppData\LocalLow\DefaultCompany\Survival Shooter\Profiler\<UTC-vreme>-<id>.raw
   ```

   Tačna putanja se ispisuje i u player log. Svako pokretanje dobija zaseban fajl.
   Posle završetka možeš izaći iz igre. Nemoj nasilno gasiti proces tokom upisa.
6. U Unity otvori **Window > Analysis > Profiler**, isključi **Record**, klikni
   **Load** i izaberi `.raw` fajl. Izaberi **CPU Usage**, pa **Timeline**.
   VAŽNO: Unity 2018.3 i pri učitavanju fajla zadržava samo poslednjih 300
   frejmova u Profiler prikazu. `.raw` fajl može sadržati celu sesiju, ali
   običan Load ne omogućava pregled svih ranijih frejmova. Čekanje da se
   završi učitavanje ne uklanja ovo ograničenje.
   Postojeći snimač pravi jedan veliki fajl; za pregled cele sledeće sesije
   u ovom Editoru potrebno je dopuniti snimač podelom u fajlove kraće od
   300 frejmova, uz indeks vremena svakog dela. To trenutno nije implementirano.
7. Za proveru ponovljivosti snimi tri odvojena Pool testa. Početne frejmove
   analiziraj zasebno od vrhova pri grupnim zamenama.

## Šta tražiti u sporom frejmu

U `BenchmarkRunner.Update` traži **Benchmark.ReplaceEnemy**. U uobičajenom
frejmu jedne grupe ima 60 poziva. To je jedan marker po zamenjenom neprijatelju,
ne jedan marker za celu grupu. U **Hierarchy** čitaj **Calls** i ukupni **Time ms**
da vidiš zbir 60 zamena. Posebno proveri **Benchmark.BeginDeath**, jer istog
frejma može počinjati smrt naredne grupe od 60 neprijatelja.

| Marker | Šta obuhvata |
| --- | --- |
| `Benchmark.ReplaceEnemy` | Despawn i spawn jednog slota, zajedno sa brojačima |
| `Benchmark.BeginDeath` | Početak zakazane smrti i dodavanje u red |
| `Enemy.Spawner.Spawn` / `Despawn` | Ceo neposredni put spawna/despawna |
| `Enemy.Object.Activate` / `Deactivate` | `SetActive`, uključujući sinhrone callback-ove i internu aktivaciju |
| `Enemy.Health.ResetForSpawn` | Ceo reset health-a, efekata, collider-a i animacije |
| `Enemy.Animator.Reset` | `Rebind` i trenutna evaluacija animacije zajedno |
| `Enemy.Animator.Rebind` | Sam `Animator.Rebind()` |
| `Enemy.Animator.UpdateZero` | Sam `Animator.Update(0f)` |
| `Enemy.Physics.ResetBody` | Postavljanje transformacije, kinematičkog stanja i brzina |
| `Enemy.Physics.ResetCollider` / `DeathCollider` | Promena collider trigger stanja pri spawn-u/smrti |
| `Enemy.Physics.FreezeForDeath` | Kinematičko stanje tela pri zakazanoj smrti |
| `Enemy.Navigation.Disable` | Isključivanje agenta pre spawna |
| `Enemy.Navigation.ActivateAndWarp` | Uključivanje, Warp, provera NavMesh-a, ResetPath i isStopped |
| `Enemy.Navigation.ResetPath` / `StopForDeath` | Čišćenje putanje pri despawn-u / zaustavljanje pri smrti |
| `Enemy.Navigation.SetDestination` | Eksplicitni zahtev za putanjom u EnemyMovement.Update, izvan spawner markera |
| `Enemy.Effects.Reset` | Reset audio izvora i čestica |
| `Enemy.Spawner.Create` | Instantiate, početna inicijalizacija i registracija objekta |
| `Enemy.Spawner.DestroyRequest` | Zahtev za Destroy; ne obuhvata kasnije odloženo uništavanje |

U Pool testu `Enemy.Spawner.Create` je očekivan u pripremi, ali ne tokom
redovnih zamena. Ako se pojavi u aktivnoj sesiji, proveri proširenja pool-a.

Markeri su ugnježdeni: **ne sabiraj vreme roditelja i njegove dece**.
`Enemy.Animator.Reset` već uključuje `Rebind` i `UpdateZero`. **Self ms**
isključuje decu; **Time ms** ih uključuje. Poredi više sporih frejmova i po jedan
okolni običan frejm, a ne samo apsolutni maksimum celog snimka.

## Kako izabrati narednu optimizaciju

- Ako dominira `Enemy.Animator.Reset`, razdvoji Rebind od UpdateZero i proveri
  reset konkretnih parametara/stanja umesto potpunog Rebind-a.
- Ako dominira `Enemy.Object.Activate/Deactivate`, proširi njihove unutrašnje
  Unity uzorke: uzrok može biti registracija animacije, collider-a ili callback,
  a ne sama C# linija SetActive.
- Ako dominiraju markeri `Enemy.Navigation.*`, razdvoji aktivaciju/Warp od
  zahteva SetDestination. Grupisani zahtevi su kandidat za raspoređivanje po fazama.
- Ako neposredni markeri `Enemy.Physics.*` nisu skupi, ipak proveri ugrađene
  `Physics.Simulate`, `Physics.Processing`, trigger callback-ove i sledeće frejmove.
  Fizika i navigacija mogu raditi odloženo ili na worker nitima; naši markeri
  mere pozive iz skripti, ne ukupan rad tih sistema.
- Ako postoje `GC.Collect` vrhovi, pogledaj `GC Alloc` i alokacije u tom i ranijim
  frejmovima. Alokacija nije isto što i kolekcija; sam ovaj snimak ne dokazuje
  da određena linija uzrokuje GC zastoj.
- Ako je `Benchmark.ReplaceEnemy` kratak, a ceo frejm spor, proveri
  `Benchmark.BeginDeath`, `Enemy.Navigation.SetDestination`, ugrađene AI/Animation
  uzorke, render nit i čekanje na druge niti.

Za dalju analizu sačuvaj `.raw` i zapiši nekoliko sporih frame indeksa, ukupno
trajanje frejma, Calls/Time ms za glavne markere i najveći trošak izvan njih.
Development build, Profiler i upis loga dodaju trošak: apsolutni FPS ne poredi
direktno sa ranijim Release/PresentMon testovima. Konačnu korist buduće izmene
ponovo potvrdi standardnim Release benchmarkom.

Reference: [Profiler window, Unity 2018.3](https://docs.unity3d.com/2018.3/Documentation/Manual/ProfilerWindow.html),
[CPU Usage, Unity 2018.3](https://docs.unity3d.com/2018.3/Documentation/Manual/ProfilerCPU.html).
