param(
    [string]$PsqlPath = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
$settings = Get-Content (Join-Path $projectDirectory 'appsettings.json') -Raw | ConvertFrom-Json
$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection.set_ConnectionString($settings.ConnectionStrings.DefaultConnection)
if (-not (Test-Path -LiteralPath $PsqlPath)) { throw 'psql.exe not found. Pass -PsqlPath with your PostgreSQL installation path.' }
$previousPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = [string]$connection.get_Item('Password')
    & $PsqlPath -X -w -v ON_ERROR_STOP=1 `
        -h ([string]$connection.get_Item('Host')) `
        -p ([string]$connection.get_Item('Port')) `
        -U ([string]$connection.get_Item('Username')) `
        -d ([string]$connection.get_Item('Database')) `
        -f (Join-Path $projectDirectory 'Data/sample-equipment.sql')
    if ($LASTEXITCODE -ne 0) { throw 'Equipment population failed. See PostgreSQL error above.' }
}
finally {
    $env:PGPASSWORD = $previousPassword
}
