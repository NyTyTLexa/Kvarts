# Docker Desktop on Windows cannot use an NTFS junction as a build context
# ("context must be a directory") and cannot use a Cyrillic path (gRPC header).
# Copy the repo to a real ASCII folder, then compose from there.
$ErrorActionPreference = "Stop"

# PS 5.1: `2>&1` on a native exe wraps every stderr line in an ErrorRecord, and
# $ErrorActionPreference='Stop' then kills the script. docker compose writes ALL
# build progress to stderr, so the whole build must run with Continue.
function Invoke-Native([scriptblock]$block) {
  $prev = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try { & $block } finally { $ErrorActionPreference = $prev }
}


$here = $PSScriptRoot
$marker = Get-Item -LiteralPath $here -Force
if ($marker.Attributes -band [IO.FileAttributes]::ReparsePoint) {
  $target = $marker.Target
  if ($target -is [array]) { $target = $target[0] }
  $here = [string]$target
}

$dst = "C:\src\ps-docker"
$oldJunction = "C:\src\procurement"

function Remove-JunctionIfAny([string]$path) {
  if (-not (Test-Path -LiteralPath $path)) { return }
  $item = Get-Item -LiteralPath $path -Force
  if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
    cmd.exe /c "rmdir `"$path`""
  }
}

if ((Get-Location).Path -like "*\src\procurement*") {
  Set-Location $here
}

New-Item -ItemType Directory -Force -Path "C:\src" | Out-Null
Remove-JunctionIfAny $oldJunction
New-Item -ItemType Directory -Force -Path $dst | Out-Null

Write-Host "Copy $here -> $dst"
& robocopy.exe $here $dst /MIR /XD node_modules bin obj dist .git /XF *.log /NFL /NDL /NJH /NJS /NP
# robocopy exit 0-7 is success
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with code $LASTEXITCODE" }

Set-Location $dst
$logDir = Join-Path $dst 'build-logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

# ASCII context: BuildKit OK. Parallel api+auth+worker+frontend OOMs Docker Desktop.
$env:COMPOSE_BAKE = "false"
$env:DOCKER_BUILDKIT = "1"
$env:COMPOSE_DOCKER_CLI_BUILD = "1"
$env:COMPOSE_PARALLEL_LIMIT = "1"
$env:BUILDX_NO_DEFAULT_ATTESTATIONS = "1"

Write-Host "Checking docker engine..."
$infoLog = Join-Path $logDir 'docker-info.txt'
Invoke-Native { & docker info 2>&1 | Out-File -FilePath $infoLog -Encoding utf8 }
if ($LASTEXITCODE -ne 0) {
  Write-Host @"

*** Docker engine is DOWN (docker info exit $LASTEXITCODE).
*** This is NOT an api / Dockerfile compile error.
*** Desktop is looping: broken D:\DockerDesktopWSL\disk\docker_data.vhdx
*** (no ext4, WSL_E_USER_VHD_ALREADY_ATTACHED, mkfs fails).

Do this, in order:
  1. Docker Desktop tray whale -> Quit  (not Restart)
  2. Double-click  D:\start-docker-fresh.cmd
  3. Wait until the whale is idle (1-2 min)
  4. docker version   (must print Engine, not 'cannot connect')
  5. .\up-docker.ps1

Do NOT Format / Reset from the Docker GUI.
Do NOT delete D:\DockerDesktopWSL
Log: $infoLog
"@
  throw "docker info failed code $LASTEXITCODE - engine down. Run D:\start-docker-fresh.cmd first."
}

function Invoke-ComposeBuild([string]$service) {
  Write-Host "`n=== build $service ===" -ForegroundColor Cyan
  $log = Join-Path $logDir ("build-{0}.log" -f $service)
  # --progress is a GLOBAL compose flag; after `build` it only prints a warning.
  Invoke-Native { & docker compose --progress plain build $service 2>&1 | Tee-Object -FilePath $log }
  if ($LASTEXITCODE -ne 0) {
    Write-Host "----- last 80 lines of $log -----" -ForegroundColor Yellow
    Get-Content -LiteralPath $log -Tail 80 -ErrorAction SilentlyContinue
    throw "build $service failed code $LASTEXITCODE. Full log: $log"
  }
}

Write-Host "Building images one by one from $pwd"
Invoke-ComposeBuild api
Invoke-ComposeBuild auth
Invoke-ComposeBuild worker
Invoke-ComposeBuild frontend

Write-Host "`n=== up ===" -ForegroundColor Cyan
& docker compose up -d @args
if ($LASTEXITCODE -ne 0) { throw "docker compose up failed with code $LASTEXITCODE" }
