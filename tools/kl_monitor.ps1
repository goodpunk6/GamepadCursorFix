$ErrorActionPreference = 'Stop'
$cs = Join-Path $env:TEMP 'InputMonitor.cs'
$log = Join-Path $env:TEMP 'input_log.txt'
Add-Type -TypeDefinition (Get-Content $cs -Raw) -Language CSharp
[InputMonitor]::Start($log)
while ($true) { Start-Sleep -Seconds 5 }
