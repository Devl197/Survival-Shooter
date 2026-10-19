param(
    [Parameter(Mandatory=$true)][string]$SourceWorkbook,
    [string]$Destination='',
    [switch]$Replace
)
$ErrorActionPreference='Stop'
if($Destination -eq ''){$Destination=Join-Path $PSScriptRoot 'Benchmark_template.xltm'}
$source=(Resolve-Path -LiteralPath $SourceWorkbook).Path
$dest=[IO.Path]::GetFullPath($Destination)
if($source -eq $dest){throw 'Source and destination must be different.'}
if([IO.Path]::GetExtension($dest) -ne '.xltm'){throw 'Destination must be .xltm.'}
if((Test-Path -LiteralPath $dest) -and -not $Replace){throw 'Template exists. Use -Replace to replace it explicitly.'}
function Get-SharedHash([string]$Path){
    $stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    $sha=[Security.Cryptography.SHA256]::Create()
    try {return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','')} finally {$stream.Dispose();$sha.Dispose()}
}
$before=Get-SharedHash $source
$scratch=Join-Path $PSScriptRoot 'tmp'
[void][IO.Directory]::CreateDirectory($scratch)
$copy=Join-Path $scratch ('clean-'+[Guid]::NewGuid().ToString('N')+'.xlsm')
$inputStream=[IO.File]::Open($source,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
$outputStream=[IO.File]::Open($copy,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try {$inputStream.CopyTo($outputStream)} finally {$inputStream.Dispose();$outputStream.Dispose()}
$app=$null;$book=$null
try {
    $app=New-Object -ComObject Excel.Application
    $app.Visible=$false
    $book=$app.Workbooks.Open($copy,0,$false)
    foreach($sheet in @('Testovi','Frejmovi')){
        $ws=$book.Worksheets($sheet)
        $lastRow=$ws.UsedRange.Row+$ws.UsedRange.Rows.Count-1
        foreach($table in $ws.ListObjects){
            if($null -ne $table.DataBodyRange){$table.DataBodyRange.Delete()}
            $table.QueryTable.RefreshOnFileOpen=$false
            if($table.ListRows.Count -ne 0){throw ('Data rows remain in '+$sheet)}
        }
        if($lastRow -gt 6){[void]$ws.Rows.Item('7:'+$lastRow).Delete()}
        [void]$ws.UsedRange
    }
    $cfg=$book.Worksheets('Podesavanja');$view=$book.Worksheets('Pregled')
    $view.Range('D4').ClearContents()
    $cfg.Range('D7').Formula='=IF(Pregled!D4="","",Pregled!D4)'
    foreach($address in @('D4','D5','D8:D13')){$cfg.Range($address).ClearContents()}
    $cfg.Range('D10').Value2='Izaberi folder i osvezi rezultate'
    $cfg.Range('D16').Value2='Izaberi glavni folder. U D5 po potrebi upisi podfolder, na primer AIOFF.'
    $cfg.Range('D21').Value2='Novu svesku prvo sacuvaj kao .xlsm u folderu za izvestaje. Cist .xltm ostaje sablon.'
    $app.CalculateFull()
    foreach($chart in $view.ChartObjects()){$chart.Chart.Refresh()}
    if($cfg.Range('D14').Value2 -ne $false){throw 'Empty template is incorrectly marked ready.'}
    if($view.Range('B10').Value2 -ne ''){throw 'Selected-run statistics were not cleared.'}
    $cfg.Activate();$cfg.Range('D4').Select()
    $app.ActiveWindow.ScrollRow=1;$app.ActiveWindow.ScrollColumn=1
    $app.DisplayAlerts=$false
    $book.SaveAs($dest,53)
    $app.DisplayAlerts=$true
    Write-Output ('Clean template: '+$dest)
    Write-Output ('VBA preserved: '+$book.HasVBProject+'; queries: '+$book.Queries.Count+'; charts: '+$view.ChartObjects().Count)
} finally {
    if($null -ne $book){$book.Close($false)}
    if($null -ne $app){$app.Quit();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($app)}
    # Remove only this unique temporary file, never recurse into directories.
    if(Test-Path -LiteralPath $copy){Remove-Item -LiteralPath $copy}
}
$after=Get-SharedHash $source
if($before -ne $after){throw 'Source workbook changed unexpectedly.'}
Write-Output ('Original report SHA256 unchanged: '+$after)
