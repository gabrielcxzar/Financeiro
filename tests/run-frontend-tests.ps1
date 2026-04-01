Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Push-Location (Join-Path $PSScriptRoot "frontend")
try {
    npm.cmd test
}
finally {
    Pop-Location
}

