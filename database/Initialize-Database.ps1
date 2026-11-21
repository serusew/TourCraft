param([string]$Server = '(localdb)\MSSQLLocalDB')
$ErrorActionPreference = 'Stop'
$settings = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$settings['Data Source'] = $Server
$settings['Initial Catalog'] = 'master'
$settings['Integrated Security'] = $true
$connection = New-Object System.Data.SqlClient.SqlConnection $settings.ConnectionString
try {
    $connection.Open()
    foreach ($file in 'schema.sql', 'seed.sql') {
        $sql = Get-Content -Encoding UTF8 -Raw -LiteralPath (Join-Path $PSScriptRoot $file)
        foreach ($batch in [regex]::Split($sql, '(?im)^\s*GO\s*$')) {
            if ([string]::IsNullOrWhiteSpace($batch)) { continue }
            $command = $connection.CreateCommand()
            try {
                $command.CommandText = $batch
                [void]$command.ExecuteNonQuery()
            } finally { $command.Dispose() }
        }
    }
    Write-Output 'TourCraft database initialized.'
} finally { $connection.Dispose() }
