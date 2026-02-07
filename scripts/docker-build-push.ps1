[CmdletBinding()]
param(
    [string]$Version = $(Get-Date -Format "yyyyMMdd"),
    [string]$ServerImage = "programercat/sina_server",
    [string]$ConsoleImage = "programercat/sina_console",
    [switch]$NoPush
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

Write-Host "Using version tag: $Version"
Write-Host "Server image: $ServerImage"
Write-Host "Console image: $ConsoleImage"

$serverDockerfile = Join-Path $repoRoot "SiNan.Server\Dockerfile"
$consoleDockerfile = Join-Path $repoRoot "SiNan.Console\Dockerfile"

if (-not (Test-Path $serverDockerfile)) {
    throw "Server Dockerfile not found at $serverDockerfile"
}
if (-not (Test-Path $consoleDockerfile)) {
    throw "Console Dockerfile not found at $consoleDockerfile"
}

function Invoke-DockerBuild {
    param(
        [string]$Dockerfile,
        [string[]]$Tags,
        [string]$Context,
        [int]$Retries = 3
    )

    for ($attempt = 1; $attempt -le $Retries; $attempt++) {
        docker build -f $Dockerfile -t $Tags[0] -t $Tags[1] $Context
        if ($LASTEXITCODE -eq 0) {
            return
        }

        if ($attempt -lt $Retries) {
            Write-Host "Build failed (attempt $attempt/$Retries). Retrying in 3 seconds..."
            Start-Sleep -Seconds 3
        }
    }

    throw "Docker build failed for $Dockerfile after $Retries attempts."
}

function Assert-ImageExists {
    param([string]$Tag)

    docker image inspect $Tag | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Image not found locally: $Tag"
    }
}

$serverTags = @(
    "${ServerImage}:$Version",
    "${ServerImage}:latest"
)
$consoleTags = @(
    "${ConsoleImage}:$Version",
    "${ConsoleImage}:latest"
)

Write-Host "Building server image..."
Invoke-DockerBuild -Dockerfile $serverDockerfile -Tags $serverTags -Context $repoRoot

Write-Host "Building console image..."
Invoke-DockerBuild -Dockerfile $consoleDockerfile -Tags $consoleTags -Context $repoRoot

if (-not $NoPush) {
    Write-Host "Pushing server image..."
    foreach ($tag in $serverTags) {
        Assert-ImageExists -Tag $tag
    }
    foreach ($tag in $serverTags) {
        docker push $tag
    }

    Write-Host "Pushing console image..."
    foreach ($tag in $consoleTags) {
        Assert-ImageExists -Tag $tag
    }
    foreach ($tag in $consoleTags) {
        docker push $tag
    }
}

Write-Host "Done."
