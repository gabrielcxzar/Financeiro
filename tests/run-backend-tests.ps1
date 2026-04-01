Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "backend\Finflow.Api.ContractTests\Finflow.Api.ContractTests.csproj"
dotnet test $project --logger "console;verbosity=minimal"

