param([string]$InputDirectory = 'output/documents', [string]$OutputDirectory = 'tmp/docs/word-pdf')
$inputDir = (Resolve-Path -LiteralPath $InputDirectory).Path
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputDir = (Resolve-Path -LiteralPath $OutputDirectory).Path
$wordApp = New-Object -ComObject Word.Application
$wordApp.Visible = $false
$wordApp.DisplayAlerts = 0
try {
    foreach ($file in Get-ChildItem -LiteralPath $inputDir -Filter '*.docx') {
        $document = $wordApp.Documents.Open($file.FullName, $false, $true)
        try {
            $document.ExportAsFixedFormat((Join-Path $outputDir ($file.BaseName + '.pdf')), 17)
            Write-Output ($file.Name + ': ' + $document.ComputeStatistics(2) + ' pages')
        } finally { $document.Close(0) }
    }
} finally {
    $wordApp.Quit()
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($wordApp)
}
