Attribute VB_Name = "Benchmark"
Option Explicit

Public Sub IzaberiFolder()
    With Application.FileDialog(4)
        .Title = "Izaberi glavni folder ili kategoriju benchmark testova"
        .InitialFileName = ThisWorkbook.Worksheets("Podesavanja").Range("D4").Value & "\"
        If .Show <> -1 Then Exit Sub
        ThisWorkbook.Worksheets("Podesavanja").Range("D4").Value = .SelectedItems(1)
        ThisWorkbook.Worksheets("Podesavanja").Range("D5").ClearContents
    End With
    OsveziRezultate
End Sub

Public Sub OsveziRezultate()
    RefreshCore False
End Sub

Public Sub UcitajTest()
    On Error GoTo Failed
    If CStr(ThisWorkbook.Names("ImportFolder").RefersToRange.Value) <> CStr(ThisWorkbook.Worksheets("Podesavanja").Range("D12").Value) Then
        RefreshCore False
        Exit Sub
    End If
    LoadCore
    Exit Sub
Failed:
    SetError Err.Description
End Sub

Private Sub RefreshCore(ByVal Quiet As Boolean)
    Dim cfg As Worksheet, tbl As ListObject, row As ListRow, selected As String, found As Boolean
    On Error GoTo Failed
    Set cfg = ThisWorkbook.Worksheets("Podesavanja")
    cfg.Range("D10").Value = "Uvoz u toku"
    Application.StatusBar = "Ucitavanje benchmark rezultata..."
    Application.Calculate
    If Not CreateObject("Scripting.FileSystemObject").FolderExists(ThisWorkbook.Names("ImportFolder").RefersToRange.Value) Then Err.Raise vbObjectError + 1, , "Folder ne postoji. Proverite Podesavanja."
    Set tbl = ThisWorkbook.Worksheets("Testovi").ListObjects("tblRuns")
    If Not tbl.QueryTable.Refresh(False) Then Err.Raise vbObjectError + 2, , "Uvoz je otkazan."
    selected = CStr(ThisWorkbook.Names("SelectedRun").RefersToRange.Value)
    For Each row In tbl.ListRows
        If row.Range.Cells(1, 1).Value = selected And row.Range.Cells(1, 4).Value = True Then found = True
    Next row
    If Not found Then
        selected = ""
        For Each row In tbl.ListRows
            If row.Range.Cells(1, 4).Value = True Then
                selected = CStr(row.Range.Cells(1, 1).Value)
                Exit For
            End If
        Next row
        ThisWorkbook.Names("SelectedRun").RefersToRange.Value = selected
    End If
    If selected = "" Then Err.Raise vbObjectError + 3, , "U folderu nema validnih Completed rezultata. Pogledajte list Testovi."
    cfg.Range("D12").Value = ThisWorkbook.Names("ImportFolder").RefersToRange.Value
    LoadCore
    Application.StatusBar = False
    Exit Sub
Failed:
    Application.StatusBar = False
    cfg.Range("D10").Value = "Greska: " & Err.Description
    Application.Calculate
    If Not Quiet Then MsgBox cfg.Range("D10").Value, vbExclamation, "Benchmark"
End Sub

Private Sub LoadCore()
    Dim cfg As Worksheet
    Set cfg = ThisWorkbook.Worksheets("Podesavanja")
    cfg.Range("D10").Value = "Ucitavanje testa"
    Application.Calculate
    If Not ThisWorkbook.Worksheets("Frejmovi").ListObjects("tblFrames").QueryTable.Refresh(False) Then Err.Raise vbObjectError + 4, , "Ucitavanje testa je otkazano."
    cfg.Range("D11").Value = ThisWorkbook.Names("SelectedRun").RefersToRange.Value
    cfg.Range("D9").Value = Now
    cfg.Range("D10").Value = "Spremno"
    Application.CalculateFull
    ThisWorkbook.Worksheets("Pregled").Activate
End Sub

Private Sub SetError(ByVal Message As String)
    ThisWorkbook.Worksheets("Podesavanja").Range("D10").Value = "Greska: " & Message
    Application.StatusBar = False
    Application.Calculate
    MsgBox Message, vbExclamation, "Benchmark"
End Sub

Public Sub IzveziZaWord()
    Dim cfg As Worksheet, ws As Worksheet, fs As Object, root As String, target As String
    Dim co As ChartObject, tmp As ChartObject, oldWidth As Double, oldHeight As Double
    Dim names As Variant, i As Long, ok As Boolean, f As Integer
    On Error GoTo Failed
    Set cfg = ThisWorkbook.Worksheets("Podesavanja")
    Set ws = ThisWorkbook.Worksheets("Pregled")
    If ThisWorkbook.Names("DataReady").RefersToRange.Value <> True Then
        RefreshCore True
        If ThisWorkbook.Names("DataReady").RefersToRange.Value <> True Then Err.Raise vbObjectError + 5, , "Izvoz nije moguc: osvezite rezultate."
    End If
    Set fs = CreateObject("Scripting.FileSystemObject")
    root = Trim(CStr(cfg.Range("D8").Value))
    If root = "" Then root = ThisWorkbook.Path & "\Slike_za_Word"
    If Not fs.FolderExists(root) Then fs.CreateFolder root
    target = root & "\" & Format(Now, "yyyymmdd-hhnnss")
    i = 1
    Do While fs.FolderExists(target)
        target = root & "\" & Format(Now, "yyyymmdd-hhnnss") & "-" & CStr(i)
        i = i + 1
    Loop
    fs.CreateFolder target
    names = Array("01_FPS", "02_P99", "03_Vreme_frejma", "04_Raspodela")
    ws.Activate
    Application.ScreenUpdating = True
    For i = 1 To 4
        Set co = ws.ChartObjects(i)
        oldWidth = co.Width: oldHeight = co.Height
        co.Width = 1100: co.Height = 650
        co.Activate
        DoEvents
        co.Chart.Refresh
        ok = co.Chart.Export(target & "\" & names(i - 1) & ".png", "PNG")
        co.Width = oldWidth: co.Height = oldHeight
        If Not ok Then Err.Raise vbObjectError + 6, , "Izvoz grafikona nije uspeo."
    Next i
    ws.Range("B4:I17").CopyPicture Appearance:=xlScreen, Format:=xlPicture
    Set tmp = ws.ChartObjects.Add(10, 10, ws.Range("B4:I17").Width, ws.Range("B4:I17").Height)
    tmp.Activate
    tmp.Chart.Paste
    DoEvents
    If Not tmp.Chart.Export(target & "\05_Tabela.png", "PNG") Then Err.Raise vbObjectError + 7, , "Izvoz tabele nije uspeo."
    tmp.Delete
    Set tmp = Nothing
    Application.CutCopyMode = False
    f = FreeFile
    Open target & "\Izvor.txt" For Output As #f
    Print #f, "RunID: " & ThisWorkbook.Names("SelectedRun").RefersToRange.Value
    Print #f, "Folder: " & ThisWorkbook.Names("ImportFolder").RefersToRange.Value
    Print #f, "Vreme izvoza: " & Format(Now, "yyyy-mm-dd hh:nn:ss")
    Print #f, "FPS = 1000 / mean(FrameTime_ms). P95/P99: PERCENTILE.INC."
    Print #f, "Poredjenje: jednaka tezina svakog izvrsavanja; prosek percentila po izvrsavanju."
    Print #f, "Vremenski grafikon: prosek i maksimum u 120 intervala."
    Close #f
    cfg.Range("D13").Value = target
    ws.Range("B2").Select
    MsgBox "Sacuvane su cetiri slike grafikona i tabela:" & vbCrLf & target, vbInformation, "Izvoz za Word"
    Exit Sub
Failed:
    On Error Resume Next
    If Not co Is Nothing Then
        If oldWidth > 0 Then co.Width = oldWidth
        If oldHeight > 0 Then co.Height = oldHeight
    End If
    If Not tmp Is Nothing Then tmp.Delete
    If f > 0 Then Close #f
    MsgBox "Izvoz nije zavrsen: " & Err.Description, vbExclamation, "Benchmark"
End Sub
