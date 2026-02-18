<#
.SYNOPSIS
    Añade la estructura de proyectos del módulo de Inteligencia Artificial (AI)
    a una solución .NET 8.0 existente de NeonSuit.RSSReader.
.DESCRIPTION
    Crea proyectos Core, Infrastructure, Application, Host y Tests.
    Configura dependencias NuGet y referencias entre proyectos.
.PARAMETER SolutionPath
    Ruta completa al .sln (ej. "C:\Proyectos\NeonSuit.RSSReader\NeonSuit.RSSReader.sln").
.EXAMPLE
    .\Add-AIModule.ps1 -SolutionPath "C:\Proyectos\NeonSuit.RSSReader\NeonSuit.RSSReader.sln"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$SolutionPath
)

$ErrorActionPreference = "Continue"  # No paramos en cada error, los capturamos
Set-StrictMode -Version Latest

Write-Host "Iniciando integración del módulo AI..." -ForegroundColor Cyan

# Validar solución
if (-not (Test-Path $SolutionPath -PathType Leaf)) {
    Write-Error "El archivo .sln no existe: $SolutionPath"
    exit 1
}

$solutionDir = Split-Path $SolutionPath -Parent
$srcDir        = Join-Path $solutionDir "src"
$modulesDir    = Join-Path $srcDir "Modules"
$testsDir      = Join-Path $solutionDir "tests"

# Crear carpetas base si no existen
@($srcDir, $modulesDir, $testsDir) | ForEach-Object {
    if (-not (Test-Path $_)) {
        New-Item -Path $_ -ItemType Directory | Out-Null
        Write-Host "Creado: $_" -ForegroundColor Green
    }
}

# Nombres y rutas
$projects = @{
    Core          = @{ Name = "NeonSuit.RSSReader.AI.Core";          Path = Join-Path $modulesDir "NeonSuit.RSSReader.AI.Core" }
    Infrastructure = @{ Name = "NeonSuit.RSSReader.AI.Infrastructure"; Path = Join-Path $modulesDir "NeonSuit.RSSReader.AI.Infrastructure" }
    Application   = @{ Name = "NeonSuit.RSSReader.AI.Application";   Path = Join-Path $modulesDir "NeonSuit.RSSReader.AI.Application" }
    Host          = @{ Name = "NeonSuit.RSSReader.AI.Host";          Path = Join-Path $modulesDir "NeonSuit.RSSReader.AI.Host" }
    Tests         = @{ Name = "NeonSuit.RSSReader.AI.Tests";         Path = Join-Path $testsDir "NeonSuit.RSSReader.AI.Tests" }
}

$csprojPaths = @{}

# Función mejorada: crea proyecto si no existe
function New-DotnetProjectSafe {
    param (
        [string]$ProjectName,
        [string]$Template,
        [string]$OutputPath,
        [string]$Framework = "net8.0"
    )
    if (Test-Path $OutputPath) {
        Write-Warning "La carpeta '$OutputPath' ya existe. Saltando creación."
        $csproj = Join-Path $OutputPath "$ProjectName.csproj"
        if (Test-Path $csproj) { return $csproj }
        else { Write-Error "Existe carpeta pero no .csproj → abortando."; exit 1 }
    }

    Write-Host "Creando proyecto: $ProjectName ($Template) → $OutputPath" -ForegroundColor Green
    $output = & dotnet new $Template -n $ProjectName -f $Framework -o $OutputPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Fallo al crear proyecto $ProjectName`n$output"
        exit 1
    }
    $csproj = Join-Path $OutputPath "$ProjectName.csproj"
    if (-not (Test-Path $csproj)) { Write-Error "No se creó $csproj"; exit 1 }
    return $csproj
}

# Crear todos los proyectos
foreach ($key in $projects.Keys) {
    $p = $projects[$key]
    $csprojPaths[$key] = New-DotnetProjectSafe -ProjectName $p.Name -Template "classlib" -OutputPath $p.Path -Framework "net8.0"
    if ($key -eq "Tests") {
        $csprojPaths[$key] = New-DotnetProjectSafe -ProjectName $p.Name -Template "xunit" -OutputPath $p.Path -Framework "net8.0"
    }
    Add-ProjectToSolution $SolutionPath $csprojPaths[$key]
}

# Función para añadir referencia (con chequeo)
function Add-ProjectReferenceSafe {
    param([string]$Source, [string]$Target)
    if (-not (Test-Path $Source) -or -not (Test-Path $Target)) {
        Write-Warning "No se puede añadir referencia: falta $Source o $Target"
        return
    }
    Write-Host "Añadiendo referencia: $Source → $Target" -ForegroundColor Yellow
    $output = & dotnet add $Source reference $Target 2>&1
    if ($LASTEXITCODE -ne 0) { Write-Warning "Fallo en referencia`n$output" }
}

# Referencias entre módulos AI
Add-ProjectReferenceSafe $csprojPaths.Infrastructure $csprojPaths.Core
Add-ProjectReferenceSafe $csprojPaths.Application $csprojPaths.Core
Add-ProjectReferenceSafe $csprojPaths.Application $csprojPaths.Infrastructure
Add-ProjectReferenceSafe $csprojPaths.Host $csprojPaths.Core
Add-ProjectReferenceSafe $csprojPaths.Host $csprojPaths.Application
Add-ProjectReferenceSafe $csprojPaths.Host $csprojPaths.Infrastructure
Add-ProjectReferenceSafe $csprojPaths.Tests $csprojPaths.Core
Add-ProjectReferenceSafe $csprojPaths.Tests $csprojPaths.Infrastructure
Add-ProjectReferenceSafe $csprojPaths.Tests $csprojPaths.Application
Add-ProjectReferenceSafe $csprojPaths.Tests $csprojPaths.Host

# Referencias a proyectos existentes (con chequeo)
$existing = @{
    Core    = Join-Path $srcDir "NeonSuit.RSSReader.Core" "NeonSuit.RSSReader.Core.csproj"
    Services = Join-Path $srcDir "NeonSuit.RSSReader.Services" "NeonSuit.RSSReader.Services.csproj"
}

foreach ($key in $existing.Keys) {
    $path = $existing[$key]
    if (Test-Path $path) {
        Add-ProjectReferenceSafe $csprojPaths.Host $path
    } else {
        Write-Warning "No encontrado proyecto existente: $key ($path)"
    }
}

# Paquetes NuGet con versiones seguras para .NET 8.0
$packages = @{
    "Microsoft.ML.OnnxRuntime"                  = "1.18.1"
    "LiteDB"                                    = "5.0.17"
    "Microsoft.Extensions.Logging.Abstractions" = "8.0.0"
    "FluentValidation"                          = "11.9.2"
    "BCrypt.Net-Next"                           = "4.0.3"
    "Microsoft.Extensions.DependencyInjection.Abstractions" = "8.0.0"
    "Microsoft.Extensions.Configuration.Abstractions"       = "8.0.0"
    "Moq"                                       = "4.20.70"
    "FluentAssertions"                          = "7.0.0-alpha.4"
    "Microsoft.NET.Test.Sdk"                    = "17.10.0"
    "xunit"                                     = "2.8.1"
    "xunit.runner.visualstudio"                 = "2.8.1"
    "coverlet.collector"                        = "6.0.2"
}

foreach ($projKey in @("Infrastructure", "Application", "Host", "Tests")) {
    $csproj = $csprojPaths[$projKey]
    foreach ($pkg in $packages.Keys) {
        $ver = $packages[$pkg]
        Write-Host "Añadiendo $pkg $ver a $projKey" -ForegroundColor Blue
        $output = & dotnet add $csproj package $pkg --version $ver 2>&1
        if ($LASTEXITCODE -ne 0) { Write-Warning "Fallo al añadir $pkg`n$output" }
    }
}

# Restaurar y compilar (con salida visible)
Write-Host "`nRestaurando paquetes..." -ForegroundColor Cyan
dotnet restore $SolutionPath

Write-Host "`nCompilando solución..." -ForegroundColor Cyan
dotnet build $SolutionPath --no-restore

Write-Host "`n¡Módulo AI integrado! Abre la solución en VS." -ForegroundColor Green