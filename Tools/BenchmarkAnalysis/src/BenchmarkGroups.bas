Attribute VB_Name = "BenchmarkGroups"
Option Explicit

' Membership is keyed by RunID, never by the current Power Query row order.
Private Const MaxGroups As Long = 8
Private Const Bins As Long = 120

Public Sub PripremiGrupe()
    Dim w As Worksheet, p As Worksheet, d As Worksheet, t As ListObject, i As Long
    For Each w In ThisWorkbook.Worksheets
        If w.Name = "Grupe" Then Err.Raise vbObjectError + 100, , "List Grupe vec postoji."
    Next w
    Set w = ThisWorkbook.Worksheets.Add(After:=ThisWorkbook.Worksheets(ThisWorkbook.Worksheets.Count))
    w.Name = "Grupe"
    Set p = ThisWorkbook.Worksheets.Add(After:=w): p.Name = "Poredjenje"
    Set d = ThisWorkbook.Worksheets.Add(After:=p): d.Name = "GrupniPodaci"
    For Each w In ArraySheets()
        w.Cells.Font.Name = "Arial": w.Cells.Font.Size = 10
        w.Columns("A").ColumnWidth = 3
    Next w
    Set w = ThisWorkbook.Worksheets("Grupe")
    w.Range("B2").Value = "Grupe testova"
    w.Range("B3").Value = "1. Osvezi spisak. 2. Upisi naziv grupe u zutu kolonu. 3. Klikni Izracunaj grupe."
    w.Range("B4").Value = "Isti naziv spaja ponavljanja. Prazna grupa iskljucuje test. Najvise 8 grupa u jednom poredjenju."
    w.Range("B5").Value = "Primeri: AI ukljucen, AI iskljucen, Scena 1. Proveri scenu, AI, alokaciju i ostale uslove u Testovi."
    w.Range("B8:H8").Value = Array("RunID", "Grupa", "Scena", "AI periodic", "Alokacija", "Kategorija", "Status")
    Set t = w.ListObjects.Add(xlSrcRange, w.Range("B8:H9"), , xlYes)
    t.Name = "tblMembership": t.TableStyle = "TableStyleMedium2"
    w.Columns("B").ColumnWidth = 63: w.Columns("C:D").ColumnWidth = 28
    w.Columns("C:D").WrapText = True
    w.Columns("E:F").ColumnWidth = 18: w.Columns("G:H").ColumnWidth = 28
    w.Columns("B:C").NumberFormat = "@"
    t.ListColumns(2).DataBodyRange.Interior.Color = RGB(255, 242, 204)
    AddGroupButton w, "Osvezi spisak", "OsveziSpisakGrupa", 20, 78
    AddGroupButton w, "Izracunaj grupe", "IzracunajGrupe", 220, 78
    AddGroupButton w, "Izvezi grupe za Word", "IzveziGrupeZaWord", 420, 78
    p.Columns("B").ColumnWidth = 40: p.Columns("C:I").ColumnWidth = 16
    p.Columns("J:Q").ColumnWidth = 11
    p.Range("B2").Value = "Poredjenje grupa"
    p.Range("B3").Formula = "=IF(GroupsReady,""Spremno za izvoz"",""Promenjen ulaz ili nema obracuna: klikni Izracunaj grupe na listu Grupe."")"
    p.Range("B4").Value = "Jednaka tezina svakog testa. SD je rasipanje FPS izmedju ponavljanja; za n=1 nije definisan."
    p.Range("B5").Value = "P95/P99 su proseci percentila po testu. Vreme: 120 intervala normalizovanog trajanja (0-100%)."
    p.Range("B6").Value = "Raspodela: prosek udela po testu. Rucne grupe mogu sadrzati razlicita podesavanja; proveri uporedivost."
    p.Range("B8:I8").Value = Array("Grupa", "Broj testova", "Prosek FPS", "SD FPS", "Prosek ms", "Prosek P95 ms", "Prosek P99 ms", "Preko 16.67 ms")
    p.Range("B9:I16").NumberFormat = "0.00": p.Range("C9:C16").NumberFormat = "0"
    p.Range("I9:I16").NumberFormat = "0.00%"
    p.Range("B9:B16").NumberFormat = "@": p.Range("B9:B16").WrapText = True
    p.Rows("9:16").RowHeight = 30
    AddGroupButton p, "Izracunaj grupe", "IzracunajGrupe", 20, 420
    AddGroupButton p, "Izvezi grupe za Word", "IzveziGrupeZaWord", 220, 420
    p.Range("B18").Value = "Grafikoni i tabela se prazne kada promenis dodelu ili osvezis izvore. Izvoz ponovo racuna grupe."
    d.Range("A1").Value = "Podaci po testu: jednaka tezina redova. I:P udeli; Q:EF vremenski intervali. Prazno = nema frejmova."
    d.Range("EN1").Value = "Snimak dodele i izvora za proveru svezine; ne menjati rucno."
    d.Range("A3:H3").Value = Array("GroupID", "Grupa", "RunID", "FPS", "Mean_ms", "P95_ms", "P99_ms", "FPS2")
    For i = 1 To 8: d.Cells(3, 8 + i).Value = "Udeo_" & i: Next i
    For i = 1 To Bins: d.Cells(3, 16 + i).Value = "Vreme_" & i: Next i
    d.Columns("A").ColumnWidth = 10: d.Columns("B").ColumnWidth = 40
    d.Columns("C").ColumnWidth = 63: d.Columns("D:P").ColumnWidth = 14
    d.Columns("B:C").NumberFormat = "@"
    d.Range("I4:P10000").NumberFormat = "0.00%"
    d.Range("D4:H10000").NumberFormat = "0.000"
    d.Range("EN2").Value = 0
    ThisWorkbook.Names.Add Name:="GroupsReady", RefersTo:="=GrupniPodaci!$EN$2"
    d.Range("EN2").Formula = "=IFERROR(AND(EN3=""Spremno"",Podesavanja!D10=""Spremno"",EO3=Podesavanja!D6,EP3=Podesavanja!D9,EQ3=ROWS(tblMembership[RunID]),ER3=ROWS(tblRuns[#All]),SUMPRODUCT(--NOT(EXACT(tblMembership[[RunID]:[Grupa]],EN5:INDEX(EO:EO,4+ROWS(tblMembership[RunID])))))=0,SUMPRODUCT(--NOT(EXACT(tblRuns[#All],EQ5:INDEX(FP:FP,4+ROWS(tblRuns[#All])))))=0),FALSE)"
    For Each w In ArraySheets()
        w.Range("B2").Font.Size = 20: w.Range("B2").Font.Bold = True
        w.Activate: ActiveWindow.DisplayGridlines = False
    Next w
    p.Range("B8:I8").Interior.Color = RGB(31, 64, 99): p.Range("B8:I8").Font.Color = vbWhite
    p.Range("B8:I8").WrapText = True: p.Rows(8).RowHeight = 32
    BuildGroupView CreateObject("Scripting.Dictionary"), 1
    SinhronizujGrupe
    ThisWorkbook.Worksheets("Grupe").Activate
End Sub

Private Function ArraySheets() As Collection
    Dim c As New Collection
    c.Add ThisWorkbook.Worksheets("Grupe")
    c.Add ThisWorkbook.Worksheets("Poredjenje")
    c.Add ThisWorkbook.Worksheets("GrupniPodaci")
    Set ArraySheets = c
End Function

Private Sub AddGroupButton(ByVal w As Worksheet, ByVal title As String, ByVal action As String, ByVal x As Double, ByVal y As Double)
    With w.Shapes.AddShape(5, x, y, 180, 26)
        .TextFrame.Characters.Text = title: .OnAction = action
        .TextFrame.Characters.Font.Color = vbWhite
        .Fill.ForeColor.RGB = RGB(31, 64, 99): .Line.Visible = False
        .Placement = xlFreeFloating
    End With
End Sub

Public Sub OsveziSpisakGrupa()
    OsveziRezultate
    SinhronizujGrupe
    ThisWorkbook.Worksheets("Grupe").Activate
End Sub

Public Sub SinhronizujGrupe()
    Dim runs As ListObject, members As ListObject, r As ListRow, m As ListRow, ids As Object, seen As Object
    Dim id As String, a As Variant, idx As Long
    Set runs = ThisWorkbook.Worksheets("Testovi").ListObjects("tblRuns")
    Set members = ThisWorkbook.Worksheets("Grupe").ListObjects("tblMembership")
    Set ids = CreateObject("Scripting.Dictionary"): Set seen = CreateObject("Scripting.Dictionary")
    If Not runs.DataBodyRange Is Nothing Then
        a = runs.DataBodyRange.Value2
        For idx = 1 To UBound(a, 1)
            id = CStr(a(idx, 1))
            If ids.Exists(id) Then Err.Raise vbObjectError + 101, , "Dupliran RunID u izvorima: " & id
            ids.Add id, idx
        Next idx
    End If
    For Each m In members.ListRows
        id = Trim(CStr(m.Range.Cells(1, 1).Value2))
        If id <> "" Then
            If seen.Exists(id) Then Err.Raise vbObjectError + 102, , "Dupliran RunID u dodeli: " & id
            seen.Add id, True
        End If
    Next m
    For Each r In runs.ListRows
        id = CStr(r.Range.Cells(1, 1).Value2)
        If Not seen.Exists(id) Then
            If members.ListRows.Count = 1 And members.DataBodyRange.Cells(1, 1).Value2 = "" And members.DataBodyRange.Cells(1, 2).Value2 = "" Then
                Set m = members.ListRows(1)
            Else
                Set m = members.ListRows.Add
            End If
            m.Range.Cells(1, 1).NumberFormat = "@": m.Range.Cells(1, 1).Value2 = id
            seen.Add id, True
        End If
    Next r
    For Each m In members.ListRows
        id = Trim(CStr(m.Range.Cells(1, 1).Value2))
        If ids.Exists(id) Then
            idx = ids(id)
            m.Range.Cells(1, 3).Value2 = a(idx, 15)
            m.Range.Cells(1, 4).Value2 = a(idx, 19)
            m.Range.Cells(1, 5).Value2 = a(idx, 3)
            m.Range.Cells(1, 6).Value2 = a(idx, 16)
            If a(idx, 4) = True Then
                m.Range.Cells(1, 7).Value2 = "Dostupan"
            Else
                m.Range.Cells(1, 7).Value2 = "Nevalidan: " & a(idx, 5)
            End If
        ElseIf id <> "" Then
            m.Range.Cells(1, 7).Value2 = "Nije u trenutnom uvozu"
        End If
    Next m
    members.ListColumns(2).DataBodyRange.Interior.Color = RGB(255, 242, 204)
    members.DataBodyRange.RowHeight = 30
    Application.CalculateFull
End Sub

Public Sub IzracunajGrupe()
    On Error GoTo Failed
    ObracunajGrupe
    ThisWorkbook.Worksheets("Poredjenje").Activate
    Exit Sub
Failed:
    MsgBox "Grupe nisu izracunate: " & Err.Description, vbExclamation, "Benchmark"
End Sub

Public Function TryGroupCalculation() As String
    On Error GoTo Failed
    ObracunajGrupe
    Exit Function
Failed:
    TryGroupCalculation = Err.Description
End Function

' Public core also allows repeatable, non-interactive verification in Excel.
Public Sub ObracunajGrupe()
    Dim runs As ListObject, members As ListObject, d As Worksheet, p As Worksheet, cfg As Worksheet
    Dim ids As Object, groups As Object, seen As Object, a As Variant, out() As Variant
    Dim m As ListRow, id As String, g As String, i As Long, j As Long, k As Long, idx As Long, n As Long, gid As Long
    Dim f As Object, fs As Object, line As String, fields As Variant, col As Long, times() As Double, ft As Double
    Dim total As Double, elapsed As Double, hist(1 To 8) As Long, sums(1 To Bins) As Double, counts(1 To Bins) As Long
    Dim bounds As Variant, last As Long, reason As String
    On Error GoTo Failed
    Set d = ThisWorkbook.Worksheets("GrupniPodaci"): Set p = ThisWorkbook.Worksheets("Poredjenje")
    Set cfg = ThisWorkbook.Worksheets("Podesavanja")
    d.Range("EN3").Value2 = "Obracun u toku": Application.CalculateFull
    If cfg.Range("D10").Value2 <> "Spremno" Or cfg.Range("D6").Value2 <> cfg.Range("D12").Value2 Then Err.Raise vbObjectError + 103, , "Prvo osvezi rezultate za izabrani folder."
    SinhronizujGrupe
    Set runs = ThisWorkbook.Worksheets("Testovi").ListObjects("tblRuns")
    Set members = ThisWorkbook.Worksheets("Grupe").ListObjects("tblMembership")
    If runs.DataBodyRange Is Nothing Then Err.Raise vbObjectError + 104, , "Nema uvezenih testova."
    a = runs.DataBodyRange.Value2
    Set ids = CreateObject("Scripting.Dictionary"): Set groups = CreateObject("Scripting.Dictionary"): groups.CompareMode = vbTextCompare
    Set seen = CreateObject("Scripting.Dictionary")
    For i = 1 To UBound(a, 1)
        If ids.Exists(CStr(a(i, 1))) Then Err.Raise vbObjectError + 105, , "Dupliran RunID u izvoru."
        ids.Add CStr(a(i, 1)), i
    Next i
    ReDim out(1 To members.ListRows.Count, 1 To 136)
    Set fs = CreateObject("Scripting.FileSystemObject")
    bounds = Array(1#, 2#, 4#, 8#, 1000# / 60#, 1000# / 30#, 50#)
    For Each m In members.ListRows
        id = Trim(CStr(m.Range.Cells(1, 1).Value2)): g = Trim(CStr(m.Range.Cells(1, 2).Value2))
        If g <> "" Then
            If Len(g) > 50 Then Err.Raise vbObjectError + 106, , "Naziv grupe moze imati najvise 50 znakova."
            If Not ids.Exists(id) Then Err.Raise vbObjectError + 107, , "Test grupe nije u trenutnom uvozu: " & id & ". Obrisi grupu da ga iskljucis."
            If seen.Exists(id) Then Err.Raise vbObjectError + 108, , "Test je vise puta dodeljen: " & id
            seen.Add id, True: idx = ids(id)
            If a(idx, 4) <> True Then Err.Raise vbObjectError + 109, , "Nevalidan test u grupi: " & id
            If Not groups.Exists(g) Then groups.Add g, groups.Count + 1
            If groups.Count > MaxGroups Then Err.Raise vbObjectError + 110, , "Najvise 8 grupa. Ostalim testovima ostavi praznu grupu."
            gid = groups(g): n = n + 1
            out(n, 1) = gid: out(n, 2) = g: out(n, 3) = id
            out(n, 4) = a(idx, 12): out(n, 5) = a(idx, 7): out(n, 6) = a(idx, 9): out(n, 7) = a(idx, 10): out(n, 8) = a(idx, 13)
            Application.StatusBar = "Grupa " & g & ": " & id
            Set f = fs.OpenTextFile(CStr(a(idx, 17)) & "measured.csv", 1)
            fields = Split(f.ReadLine, ","): col = -1
            For i = 0 To UBound(fields)
                If Replace(fields(i), """", "") = "FrameTime" Then col = i
            Next i
            If col < 0 Then Err.Raise vbObjectError + 111, , "Nedostaje FrameTime: " & id
            ReDim times(1 To CLng(a(idx, 6))): k = 0: total = 0
            Erase hist: Erase sums: Erase counts
            Do While Not f.AtEndOfStream
                line = f.ReadLine
                If Len(Trim(line)) > 0 Then
                    ' PresentMon rows are numeric except Application; quoted commas are handled.
                    fields = CsvFields(line)
                    If UBound(fields) < col Then Err.Raise vbObjectError + 112, , "Neispravan CSV red: " & id
                    line = fields(col)
                    If Not IsNumeric(Replace(line, ".", Application.International(xlDecimalSeparator))) Then Err.Raise vbObjectError + 113, , "Neispravan FrameTime: " & id
                    ft = CDbl(Replace(line, ".", Application.International(xlDecimalSeparator)))
                    If ft <= 0 Then Err.Raise vbObjectError + 114, , "FrameTime mora biti pozitivan: " & id
                    k = k + 1
                    If k > UBound(times) Then Err.Raise vbObjectError + 115, , "CSV je promenjen. Osvezi rezultate."
                    times(k) = ft: total = total + ft
                    j = 1
                    Do While j <= 7
                        If ft <= bounds(j - 1) Then Exit Do
                        j = j + 1
                    Loop
                    hist(j) = hist(j) + 1
                End If
            Loop
            f.Close: Set f = Nothing
            If k <> CLng(a(idx, 6)) Then Err.Raise vbObjectError + 116, , "Broj frejmova je promenjen. Osvezi rezultate."
            If Abs(total / k - CDbl(a(idx, 7))) > 0.0000001 Then Err.Raise vbObjectError + 117, , "CSV je promenjen. Osvezi rezultate."
            If Abs(Application.WorksheetFunction.Percentile_Inc(times, 0.99) - CDbl(a(idx, 10))) > 0.0000001 Then Err.Raise vbObjectError + 118, , "P99 se ne slaze sa uvozom. Osvezi rezultate."
            elapsed = 0
            For i = 1 To k
                j = Int(elapsed / total * Bins) + 1
                If j > Bins Then j = Bins
                sums(j) = sums(j) + times(i): counts(j) = counts(j) + 1
                elapsed = elapsed + times(i)
            Next i
            For j = 1 To 8: out(n, 8 + j) = hist(j) / k: Next j
            For j = 1 To Bins
                If counts(j) > 0 Then out(n, 16 + j) = sums(j) / counts(j)
            Next j
        End If
    Next m
    If n = 0 Then Err.Raise vbObjectError + 119, , "Upisi bar jedan naziv u kolonu Grupa."
    last = d.Cells(d.Rows.Count, 1).End(xlUp).Row
    If last >= 4 Then d.Range("A4:EF" & last).ClearContents
    d.Range("A4").Resize(UBound(out, 1), 136).Value2 = out
    BuildGroupView groups, n
    d.Range("EN5").Resize(members.ListRows.Count, 2).NumberFormat = "@"
    d.Range("EQ5").Resize(runs.Range.Rows.Count, runs.Range.Columns.Count).NumberFormat = "@"
    d.Range("EN5").Resize(members.ListRows.Count, 2).Value2 = members.DataBodyRange.Resize(, 2).Value2
    d.Range("EQ5").Resize(runs.Range.Rows.Count, runs.Range.Columns.Count).Value2 = runs.Range.Value2
    d.Range("EO3").Value2 = cfg.Range("D6").Value2: d.Range("EP3").Value2 = cfg.Range("D9").Value2
    d.Range("EQ3").Value2 = members.ListRows.Count: d.Range("ER3").Value2 = runs.Range.Rows.Count
    d.Range("EN3").Value2 = "Spremno": Application.CalculateFull
    Application.StatusBar = False
    Exit Sub
Failed:
    reason = Err.Description
    On Error Resume Next
    If Not f Is Nothing Then f.Close
    d.Range("EN3").Value2 = "Greska: " & reason
    Application.StatusBar = False: Application.CalculateFull
    On Error GoTo 0
    Err.Raise vbObjectError + 120, "BenchmarkGroups", reason
End Sub

Private Function CsvFields(ByVal line As String) As Variant
    Dim result() As String, i As Long, n As Long, quoted As Boolean, ch As String
    If InStr(line, """") = 0 Then CsvFields = Split(line, ","): Exit Function
    ReDim result(0 To 30)
    For i = 1 To Len(line)
        ch = Mid$(line, i, 1)
        If ch = """" Then
            If quoted And i < Len(line) Then
                If Mid$(line, i + 1, 1) = """" Then result(n) = result(n) & """": i = i + 1 Else quoted = False
            Else
                quoted = Not quoted
            End If
        ElseIf ch = "," And Not quoted Then
            n = n + 1
            If n > UBound(result) Then ReDim Preserve result(0 To n + 10)
        Else
            result(n) = result(n) & ch
        End If
    Next i
    If quoted Then Err.Raise vbObjectError + 121, , "Nezatvoren navodnik u CSV redu."
    ReDim Preserve result(0 To n): CsvFields = result
End Function

Private Sub BuildGroupView(ByVal groups As Object, ByVal n As Long)
    Dim p As Worksheet, d As Worksheet, key As Variant, gid As Long, r As Long, i As Long, j As Long
    Dim criteria As String, values As String, col As String, co As ChartObject, s As Series, labels As Variant
    Set p = ThisWorkbook.Worksheets("Poredjenje"): Set d = ThisWorkbook.Worksheets("GrupniPodaci")
    p.Range("B9:I16").ClearContents: p.Range("B80:Q211").ClearContents
    criteria = "GrupniPodaci!$A$4:$A$" & n + 3
    labels = Array("0-1", "1-2", "2-4", "4-8", "8-16.67", "16.67-33.33", "33.33-50", "Preko 50")
    p.Range("B80").Value2 = "Interval (ms)": p.Range("B81:B88").NumberFormat = "@"
    For i = 1 To 8: p.Cells(80 + i, 2).Value2 = labels(i - 1): Next i
    p.Range("B91").Value2 = "Trajanje (%)"
    For i = 1 To Bins: p.Cells(91 + i, 2).Value2 = (i - 0.5) / Bins: Next i
    p.Range("B92:B211").NumberFormat = "0%"
    For Each key In groups.Keys
        gid = groups(key): r = 8 + gid
        p.Cells(r, 2).Value2 = key
        p.Cells(r, 3).Formula = "=IF(GroupsReady,COUNTIF(" & criteria & "," & gid & "),"""")"
        For j = 4 To 8
            Select Case j
                Case 4: col = "D"
                Case 6: col = "E"
                Case 7: col = "F"
                Case 8: col = "G"
            End Select
            If j <> 5 Then p.Cells(r, j).Formula = "=IF(GroupsReady,AVERAGEIF(" & criteria & "," & gid & ",GrupniPodaci!$" & col & "$4:$" & col & "$" & n + 3 & "),"""")"
        Next j
        p.Cells(r, 5).Formula = "=IF(AND(GroupsReady,C" & r & ">1),SQRT(MAX(0,(SUMIF(" & criteria & "," & gid & ",GrupniPodaci!$H$4:$H$" & n + 3 & ")-C" & r & "*D" & r & "^2)/(C" & r & "-1))),"""")"
        ' Histogram columns C:J correspond to group IDs; upper three bins exceed 16.67 ms.
        p.Cells(r, 9).Formula = "=IF(GroupsReady,SUM(" & p.Cells(86, gid + 2).Address(False, False) & ":" & p.Cells(88, gid + 2).Address(False, False) & "),"""")"
        p.Cells(80, gid + 2).Value2 = key: p.Cells(91, gid + 2).Value2 = key
        For i = 1 To 8
            values = "GrupniPodaci!" & d.Range(d.Cells(4, i + 8), d.Cells(n + 3, i + 8)).Address
            p.Cells(80 + i, gid + 2).Formula = "=IF(GroupsReady,AVERAGEIF(" & criteria & "," & gid & "," & values & "),NA())"
        Next i
        For i = 1 To Bins
            values = "GrupniPodaci!" & d.Range(d.Cells(4, i + 16), d.Cells(n + 3, i + 16)).Address
            p.Cells(91 + i, gid + 2).Formula = "=IF(GroupsReady,IFERROR(AVERAGEIF(" & criteria & "," & gid & "," & values & "),NA()),NA())"
        Next i
    Next key
    p.Range("C81:J88").NumberFormat = "0.00%"
    For Each co In p.ChartObjects: co.Delete: Next co
    Set co = NewGroupChart(p, "Prosek FPS po grupi", 20, 460, xlColumnClustered)
    If groups.Count > 0 Then
        Set s = co.Chart.SeriesCollection.NewSeries: s.Name = "Prosek FPS"
        s.XValues = "=" & p.Range("B9:B" & 8 + groups.Count).Address(External:=True)
        s.Values = "=" & p.Range("D9:D" & 8 + groups.Count).Address(External:=True)
        co.Chart.Axes(xlValue).MinimumScale = 0
    End If
    Set co = NewGroupChart(p, "Prosek P99 po grupi (ms)", 580, 460, xlColumnClustered)
    If groups.Count > 0 Then
        Set s = co.Chart.SeriesCollection.NewSeries: s.Name = "Prosek P99"
        s.XValues = "=" & p.Range("B9:B" & 8 + groups.Count).Address(External:=True)
        s.Values = "=" & p.Range("H9:H" & 8 + groups.Count).Address(External:=True)
        co.Chart.Axes(xlValue).MinimumScale = 0
    End If
    Set co = NewGroupChart(p, "Vreme frejma kroz trajanje testa (ms)", 20, 790, xlXYScatterLinesNoMarkers)
    For Each key In groups.Keys
        gid = groups(key)
        Set s = co.Chart.SeriesCollection.NewSeries: s.Name = "=" & p.Cells(91, gid + 2).Address(External:=True)
        s.XValues = "=" & p.Range("B92:B211").Address(External:=True)
        s.Values = "=" & p.Range(p.Cells(92, gid + 2), p.Cells(211, gid + 2)).Address(External:=True)
    Next key
    If groups.Count > 0 Then
        co.Chart.HasLegend = True: co.Chart.Legend.Position = xlLegendPositionBottom
        co.Chart.Axes(xlCategory).TickLabels.NumberFormat = "0%"
        co.Chart.Axes(xlCategory).MinimumScale = 0: co.Chart.Axes(xlCategory).MaximumScale = 1
    End If
    Set co = NewGroupChart(p, "Raspodela vremena frejma po grupi", 580, 790, xlColumnClustered)
    For Each key In groups.Keys
        gid = groups(key)
        Set s = co.Chart.SeriesCollection.NewSeries: s.Name = "=" & p.Cells(80, gid + 2).Address(External:=True)
        s.XValues = "=" & p.Range("B81:B88").Address(External:=True)
        s.Values = "=" & p.Range(p.Cells(81, gid + 2), p.Cells(88, gid + 2)).Address(External:=True)
    Next key
    If groups.Count > 0 Then
        co.Chart.HasLegend = True: co.Chart.Legend.Position = xlLegendPositionBottom
        co.Chart.Axes(xlCategory).CategoryType = xlCategoryScale
        co.Chart.Axes(xlCategory).TickLabels.NumberFormat = "General"
        co.Chart.Axes(xlValue).TickLabels.NumberFormat = "0%"
    End If
End Sub

Private Function NewGroupChart(ByVal p As Worksheet, ByVal title As String, ByVal x As Double, ByVal y As Double, ByVal kind As Long) As ChartObject
    Dim co As ChartObject
    Set co = p.ChartObjects.Add(x, y, 545, 315)
    co.Chart.ChartType = kind: co.Chart.HasTitle = True: co.Chart.ChartTitle.Text = title
    co.Chart.HasLegend = False: co.Chart.ChartArea.Font.Name = "Arial": co.Chart.ChartArea.Font.Size = 10
    co.Chart.DisplayBlanksAs = xlNotPlotted
    Set NewGroupChart = co
End Function

Public Sub IzveziGrupeZaWord()
    On Error GoTo Failed
    Dim target As String
    target = ExportGroupImages()
    MsgBox "Grafikoni, tabela i izvor su sacuvani:" & vbCrLf & target, vbInformation, "Grupe za Word"
    Exit Sub
Failed:
    MsgBox "Izvoz grupa nije zavrsen: " & Err.Description, vbExclamation, "Benchmark"
End Sub

Public Function ExportGroupImages() As String
    Dim p As Worksheet, cfg As Worksheet, fs As Object, root As String, target As String, i As Long
    Dim co As ChartObject, tmp As ChartObject, ow As Double, oh As Double, f As Object, m As ListRow, reason As String
    On Error GoTo Failed
    ObracunajGrupe
    Set p = ThisWorkbook.Worksheets("Poredjenje"): Set cfg = ThisWorkbook.Worksheets("Podesavanja")
    Set fs = CreateObject("Scripting.FileSystemObject")
    root = Trim(CStr(cfg.Range("D8").Value2))
    If root = "" Then
        If ThisWorkbook.Path = "" Then Err.Raise vbObjectError + 122, , "Prvo sacuvaj radni izvestaj kao .xlsm."
        root = ThisWorkbook.Path & "\Slike_za_Word"
    End If
    If Not fs.FolderExists(root) Then fs.CreateFolder root
    target = root & "\Grupe-" & Format(Now, "yyyymmdd-hhnnss")
    Do While fs.FolderExists(target)
        i = i + 1: target = root & "\Grupe-" & Format(Now, "yyyymmdd-hhnnss") & "-" & i
    Loop
    fs.CreateFolder target
    p.Activate: Application.ScreenUpdating = True
    For i = 1 To p.ChartObjects.Count
        Set co = p.ChartObjects(i): ow = co.Width: oh = co.Height
        co.Width = 1100: co.Height = 650: co.Activate: DoEvents: co.Chart.Refresh
        If Not co.Chart.Export(target & "\0" & i & "_Grupe.png", "PNG") Then Err.Raise vbObjectError + 123, , "Izvoz grafikona nije uspeo."
        co.Width = ow: co.Height = oh
    Next i
    p.Range("B2:I16").CopyPicture Appearance:=xlScreen, Format:=xlPicture
    Set tmp = p.ChartObjects.Add(20, 20, p.Range("B2:I16").Width, p.Range("B2:I16").Height)
    tmp.Activate: tmp.Chart.Paste: DoEvents
    If Not tmp.Chart.Export(target & "\05_Tabela_grupa.png", "PNG") Then Err.Raise vbObjectError + 124, , "Izvoz tabele nije uspeo."
    tmp.Delete: Set tmp = Nothing
    Set f = fs.CreateTextFile(target & "\Izvor_grupa.txt", True, True)
    f.WriteLine "Folder: " & cfg.Range("D6").Value2
    f.WriteLine "Izvoz: " & Format(Now, "yyyy-mm-dd hh:nn:ss")
    f.WriteLine "Jednaka tezina po testu. FPS=1000/mean(ms). P95/P99=prosek percentila pojedinacnih testova."
    f.WriteLine "SD FPS je uzoracki SD izmedju ponavljanja; n=1 nema SD. Raspodela=prosek udela."
    f.WriteLine "Vreme: 120 intervala normalizovanog kumulativnog FrameTime; prazni intervali izostavljeni iz proseka."
    For Each m In ThisWorkbook.Worksheets("Grupe").ListObjects("tblMembership").ListRows
        If Trim(CStr(m.Range.Cells(1, 2).Value2)) <> "" Then f.WriteLine m.Range.Cells(1, 2).Value2 & vbTab & m.Range.Cells(1, 1).Value2
    Next m
    f.Close: Set f = Nothing
    cfg.Range("D13").Value2 = target: Application.CutCopyMode = False
    ExportGroupImages = target
    Exit Function
Failed:
    reason = Err.Description
    On Error Resume Next
    If Not co Is Nothing Then co.Width = ow: co.Height = oh
    If Not tmp Is Nothing Then tmp.Delete
    If Not f Is Nothing Then f.Close
    On Error GoTo 0
    Err.Raise vbObjectError + 125, "BenchmarkGroups", reason
End Function
