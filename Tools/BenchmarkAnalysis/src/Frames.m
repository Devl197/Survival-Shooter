let
    ReadFrames = (Folder as text) as table => let
    M = Json.Document(File.Contents(Folder & "session.json")),
    Csv = Table.PromoteHeaders(Csv.Document(File.Contents(Folder & "measured.csv"),[Delimiter=",",Encoding=65001,QuoteStyle=QuoteStyle.Csv]),[PromoteAllScalars=true]),
    Needed = Table.SelectColumns(Csv,{"CPUStartQPC","FrameTime"}),
    Typed = Table.TransformColumnTypes(Needed,{{"CPUStartQPC",Int64.Type},{"FrameTime",type number}},"en-US"),
    Checked = if Table.RowCount(Typed)<>M[filteredFrames] or Table.RowCount(Typed)=0 then error "Broj frejmova se ne slaze" else if List.AnyTrue(List.Transform(Typed[FrameTime],each _=null or _<=0 or Number.IsNaN(_) or _=#infinity)) then error "Neispravno FrameTime" else Typed,
    Time = Table.AddColumn(Checked,"Time_s",each ([CPUStartQPC]-M[startQpc])/M[frequency],type number),
    Renamed = Table.RenameColumns(Time,{{"FrameTime","FrameTime_ms"}}),
    Index = Table.AddIndexColumn(Renamed,"Frame",1,1,Int64.Type),
    Ordered = Table.ReorderColumns(Index,{"Frame","Time_s","FrameTime_ms","CPUStartQPC"})
    in Ordered,
    Result = ReadFrames(Text.From(Excel.CurrentWorkbook(){[Name="SelectedFolder"]}[Content]{0}[Column1]))
in Result
