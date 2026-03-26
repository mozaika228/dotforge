param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("bootstrap", "restore", "build", "test", "format", "ci")]
    [string]$Task
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Push-Location $Root

try {
    switch ($Task) {
        "bootstrap" {
            dotnet --info
            dotnet restore dotforge.sln --use-lock-file
        }
        "restore" {
            dotnet restore dotforge.sln --use-lock-file
        }
        "build" {
            dotnet build dotforge.sln --configuration Release --no-restore
        }
        "test" {
            dotnet test dotforge.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory ./TestResults
        }
        "format" {
            dotnet format dotforge.sln --verify-no-changes
        }
        "ci" {
            dotnet restore dotforge.sln --use-lock-file
            dotnet build dotforge.sln --configuration Release --no-restore
            dotnet test dotforge.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory ./TestResults
        }
    }
}
finally {
    Pop-Location
}
