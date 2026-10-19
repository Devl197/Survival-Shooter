let
    Root = Text.TrimEnd(Text.From(Excel.CurrentWorkbook(){[Name="ImportFolder"]}[Content]{0}[Column1]), {"\", "/"}),
    Files = Folder.Files(Root),
    Sessions = Table.SelectRows(Files, each Text.Lower([Name]) = "session.json"),
    ReadRun = (f as record) as record => let
        Folder = f[Folder Path],
        RunID = List.Last(Text.Split(Text.TrimEnd(Folder, {"\", "/"}), "\")),
        J = try Json.Document(f[Content]),
        M = if J[HasError] then [] else J[Value],
        Get = (k as text) => Record.FieldOrDefault(M,k,null),
        Str = (k as text) => if Get(k)=null then "Nepoznato" else Text.From(Get(k),"en-US"),
        Category = Text.Trim(Text.Replace(Text.BeforeDelimiter(Folder,RunID,{0,RelativePosition.FromEnd}),Root,""),{"\","/"}),
        Result = Get("result"),
        Csv = try Table.PromoteHeaders(Csv.Document(File.Contents(Folder & "measured.csv"),[Delimiter=",",Encoding=65001,QuoteStyle=QuoteStyle.Csv]),[PromoteAllScalars=true]),
        Analysis = try (let
            T = if Csv[HasError] then error "measured.csv nedostaje ili nije citljiv" else Csv[Value],
            A = List.Buffer(List.Transform(Table.Column(T,"FrameTime"),each Number.FromText(_,"en-US"))),
            N = List.Count(A),
            Checked = if N=0 then error "Nema frejmova" else if List.AnyTrue(List.Transform(A,each _=null or _<=0 or Number.IsNaN(_) or _=#infinity)) then error "Neispravno FrameTime" else if Get("filteredFrames")=null or N<>Number.From(Get("filteredFrames")) then error "Broj frejmova se ne slaze sa metapodacima" else A,
            Mean = List.Average(Checked),
            P = List.Percentile(Checked,{0.5,0.95,0.99},[PercentileMode=PercentileMode.ExcelInc])
        in [Frames=N,Mean_ms=Mean,Median_ms=P{0},P95_ms=P{1},P99_ms=P{2},Max_ms=List.Max(Checked),FPS=1000/Mean,FPS2=Number.Power(1000/Mean,2)]),
        Valid = not J[HasError] and Result="Completed" and not Analysis[HasError],
        Reason = if J[HasError] then "Neispravan session.json" else if Result<>"Completed" then "Status: " & Str("result") else if Analysis[HasError] then Analysis[Error][Message] else "",
        Required = {"scene","scenario","scenarioConfiguration","replayFile","seed","enemyCount","duration","enemyCullingOverridden","animatorCullingMode","updateWhenOffscreen","skinnedMotionVectors","periodicPathUpdates","pathUpdateIntervalSeconds","vSyncCount","targetFrameRate","toolVersion","toolSha256"},
        Known = List.AllTrue(List.Transform(Required,each Record.HasFields(M,_) and Record.Field(M,_)<>null)),
        Key = if Known then Category & "|" & Text.FromBinary(Json.FromValue(Record.SelectFields(M,Required)),TextEncoding.Utf8) else "Nepotpuni metapodaci|" & Folder,
        S = if Valid then Analysis[Value] else [Frames=null,Mean_ms=null,Median_ms=null,P95_ms=null,P99_ms=null,Max_ms=null,FPS=null,FPS2=null]
    in Record.Combine({[RunID=RunID,Scenario=Str("scenario"),Allocation=Str("allocation"),Valid=Valid,Reason=Reason],S,[GroupKey=Key,Scene=Str("scene"),Category=if Category="" then "Glavni folder" else Category,Folder=Folder,Duration_s=Get("duration"),AI_periodic=Str("periodicPathUpdates"),AI_interval_s=Get("pathUpdateIntervalSeconds"),Culling=Str("animatorCullingMode"),EnemyCount=Get("enemyCount"),Replay=Str("replayFile"),Configuration=Str("scenarioConfiguration"),MetadataComplete=Known]}),
    Columns={"RunID","Scenario","Allocation","Valid","Reason","Frames","Mean_ms","Median_ms","P95_ms","P99_ms","Max_ms","FPS","FPS2","GroupKey","Scene","Category","Folder","Duration_s","AI_periodic","AI_interval_s","Culling","EnemyCount","Replay","Configuration","MetadataComplete"},
    Data = Table.FromRecords(List.Transform(Table.ToRecords(Sessions),each ReadRun(_)),Columns,MissingField.UseNull),
    Typed = Table.TransformColumnTypes(Data,{{"RunID",type text},{"Scenario",type text},{"Allocation",type text},{"Valid",type logical},{"Reason",type text},{"Frames",Int64.Type},{"Mean_ms",type number},{"Median_ms",type number},{"P95_ms",type number},{"P99_ms",type number},{"Max_ms",type number},{"FPS",type number},{"FPS2",type number},{"GroupKey",type text},{"Scene",type text},{"Category",type text},{"Folder",type text},{"Duration_s",type number},{"AI_periodic",type text},{"AI_interval_s",type number},{"Culling",type text},{"EnemyCount",Int64.Type},{"Replay",type text},{"Configuration",type text},{"MetadataComplete",type logical}},"en-US"),
    Keys = Table.AddIndexColumn(Table.Sort(Table.Distinct(Table.SelectColumns(Typed,{"GroupKey"})),{{"GroupKey",Order.Ascending}}),"GroupID",1,1,Int64.Type),
    Joined = Table.NestedJoin(Typed,{"GroupKey"},Keys,{"GroupKey"},"K",JoinKind.LeftOuter),
    Expanded = Table.ExpandTableColumn(Joined,"K",{"GroupID"}),
    Renamed = Table.RenameColumns(Expanded,{{"GroupKey","ComparisonSettings"},{"GroupID","GroupKey"}}),
    Sorted = Table.Sort(Renamed,{{"RunID",Order.Descending}})
in Sorted
