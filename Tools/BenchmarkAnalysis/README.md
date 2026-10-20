# Benchmark Analysis

Excel alat za analizu PresentMon merenja iz projekta Survival Shooter. Zahteva desktop Microsoft 365 Excel na Windows-u. Za upotrebu nisu potrebni Node.js, Python ili dodatni paketi.

## Sadržaj za verzionisanje

- `Benchmark_template.xltm`: čist Excel šablon sa makroima, Power Query uvozom, pregledom jednog testa i poređenjem proizvoljnih grupa, bez učitanih merenja i lokalnih putanja.
- `src/Benchmark.bas`: VBA kod dugmadi za izbor foldera, uvoz i izvoz.
- `src/BenchmarkGroups.bas`: dodela testova grupama, obračun i izvoz grupnih grafikona.
- `src/Runs.m`: Power Query obrada pojedinačnih izvršavanja.
- `src/Frames.m`: Power Query uvoz frejmova izabranog testa.
- `New-CleanTemplate.ps1`: pomoćna skripta za pripremu čistog šablona iz radnog izveštaja. Izvorni izveštaj ostaje neizmenjen.

Šablon i tekstualni izvori zajedno predstavljaju alat. Promene VBA/Power Query koda u šablonu uskladi sa odgovarajućim fajlovima u `src/` pre commit-a. Skripta za čišćenje čuva već ugrađeni kod i ne uvozi automatski te izvore.

## Korišćenje

1. Dvoklikom na `Benchmark_template.xltm` napravi novu radnu svesku. Pre uvoza je sačuvaj kao **Excel Macro-Enabled Workbook (.xlsm)** u `outputs/benchmark-excel/` ili drugom folderu za izveštaje van Git-a. Nemoj prepisivati čist šablon.
2. Ako Excel zatraži, odobri makroe i lokalne izvore za ovu svesku.
3. Na listu **Podesavanja** klikni **Izaberi folder**, ili unesi glavni folder i opcioni podfolder. Prazan podfolder uključuje sve potfoldere. Uobičajena lokacija rezultata igre je `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Survival Shooter\Benchmarks`.
4. Klikni **Osvezi rezultate**. Dugme redom osvežava sesije, izabrani test i grafikone. Uvoz zamenjuje prethodne rezultate.
5. Na listu **Pregled**, u ćeliji **D4**, izaberi RunID i klikni **Ucitaj izabrani test**.
6. Klikni **Izvezi za Word**. Četiri grafikona, tabela i podaci o izvoru čuvaju se u novom vremenski označenom podfolderu `Slike_za_Word` pored radnog izveštaja. Drugi izlazni folder možeš zadati u Podesavanja.
7. U Wordu koristi **Insert > Pictures > This Device** za PNG slike. Tabelu možeš i direktno kopirati iz Excela.

## Grupe i poređenje ponavljanja

1. Na listu **Grupe** klikni **Osvezi spisak**. Uvozi testove iz podešenog foldera i dodaje nove RunID vrednosti. Ranije dodele ostaju vezane za RunID i posle sortiranja ili ponovnog uvoza.
2. U žutu kolonu **Grupa** upiši isti naziv za sva ponavljanja koja želiš da objediniš, npr. `Level 1 / AI ON`, `Level 1 / AI OFF`, `Scena 2`. Nazivi se porede bez obzira na velika/mala slova i spoljne razmake. Prazno polje isključuje test. Najviše osam grupa (naziv do 50 znakova) prikazuje se istovremeno.
3. Klikni **Izracunaj grupe**. List **Poredjenje** prikazuje broj testova, srednji FPS, uzorački SD FPS, srednje vreme frejma, prosek P95/P99 i prosečan udeo frejmova preko 16,67 ms. Četiri zajednička grafikona porede FPS, P99, tok vremena frejma i raspodelu.
4. **Izvezi grupe za Word** ponovo računa grupe iz lokalnih CSV fajlova i izvozi četiri PNG grafikona, tabelu i `Izvor_grupa.txt` sa imenima grupa i njihovim RunID vrednostima. Izvoz ide u novi podfolder `Slike_za_Word/Grupe-...`, odnosno u izabrani izlazni folder.

Promena dodele, foldera ili učitanih izvora prazni rezultate dok ponovo ne klikneš **Izracunaj grupe**. Test koji nije u trenutnom uvozu ostaje u spisku sa objašnjenjem. Ako je dodeljen grupi, obračun se zaustavlja: ponovo uvezi njegov folder ili obriši naziv grupe da ga isključiš. Duplirani RunID i nevalidna merenja takođe blokiraju obračun. Ne menjaj RunID kolonu; uređuj žutu kolonu i sortiraj celu Excel tabelu.

**GrupniPodaci** sadrži proverljive rezultate po testu. Zbirna tabela i serije grafikona koriste Excel formule `COUNTIF`, `AVERAGEIF`, `SUMIF` i `SQRT`. Snimak ulaza desno na tom listu služi proveri svežine i nije za ručno menjanje.

## Metod obračuna i ograničenja

- Izvorni `session.json` i `measured.csv` se ne menjaju. U statistiku ulaze samo `Completed` sesije sa validnim pozitivnim vremenima frejmova i brojem frejmova usaglašenim sa metapodacima. Odbačene sesije imaju razlog u listu Testovi.
- FPS je `1000 / prosečno vreme frejma u ms`. Percentili koriste metod `PERCENTILE.INC`.
- Poređenje Pool i InstantiateDestroy režima koristi izvršavanja sa istim zabeleženim podešavanjima i kategorijom foldera. Svako izvršavanje ima jednaku težinu. P95/P99 poređenja su proseci percentila pojedinačnih izvršavanja. SD FPS se računa između izvršavanja i zahteva najmanje dva ponavljanja.
- Izveštaji bez ključnih metapodataka ostaju odvojene grupe. Nedostajući režim alokacije ostaje prazan.
- Hardver, rezolucija i sadržaj replay fajla nisu potpuno identifikovani postojećim metapodacima. Koristi iste spoljne uslove i zasebne podfoldere za uporediva merenja.
- Vremenski grafikon i raspodela prikazuju **jedno izabrano izvršavanje**. Vremenski grafikon prikazuje prosek i maksimum u 120 intervala. Svi frejmovi su na listu Frejmovi.
- U ručnim grupama svaki test ima jednaku težinu bez obzira na broj frejmova. FPS grupe je prosek FPS pojedinačnih testova, a ne `1000 / prosečno vreme svih frejmova`. P95/P99 su proseci percentila, a ne percentili spojenog skupa. SD se prikazuje tek za najmanje dva testa.
- Grupna raspodela je prosek udela po testu; njen zbir je 100%. Grupni vremenski grafikon deli kumulativni zbir FrameTime svakog testa na 120 intervala od 0 do 100% trajanja, pa usrednjava proseke intervala između testova. Prazni intervali izostaju iz tog proseka. Ovo omogućava poređenje različitih trajanja, ali ne poravnava konkretne događaje u različitim scenama.
- Ručne grupe namerno dozvoljavaju poređenje različitih scena/AI režima; ne proveravaju automatski jednake spoljne uslove. Metapodatak `AI_periodic` označava periodično ažuriranje putanja, ne nužno isključenje celog AI sistema. Nazive grupa uskladi sa stvarnim eksperimentom.
- Histogram koristi intervale `(donja, gornja]` ms, uz poslednji interval `> 50 ms`. Oznake intervala su tekst radi sprečavanja Excel konverzije u datume.

## Generisani fajlovi

Glavni `.gitignore` izuzima `outputs/`, lokalne korenske foldere `Benchmarks/` i `Profiler/`, kao i `results/`, `reports/`, `exports/`, `Slike_za_Word/`, `tmp/` i radne `.xlsm` fajlove unutar ovog alata. Čisti `.xltm` šablon ostaje namenjen verzionisanju. Ne koristi `git add -f` za rezultate.

Postojeći popunjeni izveštaj je sačuvan u `outputs/benchmark-excel/Benchmark_sablon.xlsm`, zajedno sa ranije izvezenim slikama. Ti podaci su lokalni i ignorisani. Za backup rezultata koristi zasebnu kopiju; `.gitignore` nije backup.

Proširena radna kopija je `outputs/benchmark-excel/Benchmark_grupe.xlsm`. Sadrži tri početne grupe prema dostupnoj sceni i AI metapodatku (3 + 3 + 1 test); ostala izvršavanja nisu automatski dodeljena. To je primer upotrebe, ne potvrda kontrolisanog eksperimenta.

## Obnavljanje čistog šablona

Iz PowerShell-a, uz instaliran Excel:

```powershell
.\Tools\BenchmarkAnalysis\New-CleanTemplate.ps1 -SourceWorkbook .\outputs\benchmark-excel\Benchmark_grupe.xlsm -Replace
```

Ako ciljni šablon već postoji, skripta zahteva `-Replace`. Radi na privremenoj kopiji, prazni ulazne tabele, dodele grupa, rezultate, snimke izvora i lokalna podešavanja, uklanja grupne serije iz grafikona i čuva `.xltm`. Ne osvežava izvore, ne izvršava makroe i ne menja izvorni izveštaj. Ne menja Excel/Windows bezbednosna podešavanja. Za ažuriranje VBA izvora koristi Excelov VBA editor i uskladi oba `.bas` modula; pristup VBA projektu iz automatizacije nije potreban.
