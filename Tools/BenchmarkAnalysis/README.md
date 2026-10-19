# Benchmark Analysis

Excel alat za analizu PresentMon merenja iz projekta Survival Shooter. Zahteva desktop Microsoft 365 Excel na Windows-u. Za upotrebu nisu potrebni Node.js, Python ili dodatni paketi.

## Sadržaj za verzionisanje

- `Benchmark_template.xltm`: čist Excel šablon sa makroima, Power Query uvozom, formulama i četiri grafikona, bez učitanih merenja i lokalnih putanja.
- `src/Benchmark.bas`: VBA kod dugmadi za izbor foldera, uvoz i izvoz.
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

## Metod obračuna i ograničenja

- Izvorni `session.json` i `measured.csv` se ne menjaju. U statistiku ulaze samo `Completed` sesije sa validnim pozitivnim vremenima frejmova i brojem frejmova usaglašenim sa metapodacima. Odbačene sesije imaju razlog u listu Testovi.
- FPS je `1000 / prosečno vreme frejma u ms`. Percentili koriste metod `PERCENTILE.INC`.
- Poređenje Pool i InstantiateDestroy režima koristi izvršavanja sa istim zabeleženim podešavanjima i kategorijom foldera. Svako izvršavanje ima jednaku težinu. P95/P99 poređenja su proseci percentila pojedinačnih izvršavanja. SD FPS se računa između izvršavanja i zahteva najmanje dva ponavljanja.
- Izveštaji bez ključnih metapodataka ostaju odvojene grupe. Nedostajući režim alokacije ostaje prazan.
- Hardver, rezolucija i sadržaj replay fajla nisu potpuno identifikovani postojećim metapodacima. Koristi iste spoljne uslove i zasebne podfoldere za uporediva merenja.
- Vremenski grafikon i raspodela prikazuju **jedno izabrano izvršavanje**. Vremenski grafikon prikazuje prosek i maksimum u 120 intervala. Svi frejmovi su na listu Frejmovi.
- Proizvoljno imenovane grupe za poređenje različitih AI režima/scena još nisu implementirane.
- Histogram koristi intervale `(donja, gornja]` ms, uz poslednji interval `> 50 ms`. Oznake intervala su tekst radi sprečavanja Excel konverzije u datume.

## Generisani fajlovi

Glavni `.gitignore` izuzima `outputs/`, lokalne korenske foldere `Benchmarks/` i `Profiler/`, kao i `results/`, `reports/`, `exports/`, `Slike_za_Word/`, `tmp/` i radne `.xlsm` fajlove unutar ovog alata. Čisti `.xltm` šablon ostaje namenjen verzionisanju. Ne koristi `git add -f` za rezultate.

Postojeći popunjeni izveštaj je sačuvan u `outputs/benchmark-excel/Benchmark_sablon.xlsm`, zajedno sa ranije izvezenim slikama. Ti podaci su lokalni i ignorisani. Za backup rezultata koristi zasebnu kopiju; `.gitignore` nije backup.

## Obnavljanje čistog šablona

Iz PowerShell-a, uz instaliran Excel:

```powershell
.\Tools\BenchmarkAnalysis\New-CleanTemplate.ps1 -SourceWorkbook .\outputs\benchmark-excel\Benchmark_sablon.xlsm
```

Ako ciljni šablon već postoji, skripta zahteva `-Replace`. Radi na privremenoj kopiji, prazni ulazne tabele i lokalna podešavanja, preračunava prikaze i čuva `.xltm`. Ne osvežava izvore, ne izvršava makroe i ne menja izvorni izveštaj. Ne menja Excel/Windows bezbednosna podešavanja.
