<#
.SYNOPSIS
  Chạy thử EduViet ở chế độ demo (SQLite + dữ liệu mẫu), không cần SQL Server.

.DESCRIPTION
  1. Kiểm tra .NET 10 SDK và Node.js 22+.
  2. Lần đầu: sinh khóa JWT ngẫu nhiên và hỏi mật khẩu demo, lưu bằng User Secrets (không ghi vào repository).
  3. Cài gói npm nếu chưa có.
  4. Mở hai cửa sổ: API (http://localhost:5080) và web (http://localhost:5173).

.PARAMETER ResetData
  Xóa DB demo để tạo lại dữ liệu mẫu từ đầu.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File setup\run-demo.ps1
#>
param([switch]$ResetData)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$api = Join-Path $root 'src\backend\src\OnlineLearning.API'
$web = Join-Path $root 'src\frontend\online-learning-client'

function Fail($msg) { Write-Host "LỖI: $msg" -ForegroundColor Red; exit 1 }

# 1. Công cụ
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail 'Chưa cài .NET SDK. Xem soft\README.md' }
if (-not ((dotnet --list-sdks) -match '^10\.')) { Fail 'Cần .NET SDK 10.x (dotnet --list-sdks). Xem soft\README.md' }
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { Fail 'Chưa cài Node.js. Xem soft\README.md' }
$nodeMajor = [int]((node -v).TrimStart('v').Split('.')[0])
if ($nodeMajor -lt 22) { Fail "Cần Node.js 22 trở lên (đang có $(node -v))" }
Write-Host 'Đã có .NET 10 SDK và Node.js.' -ForegroundColor Green

# 2. Bí mật (User Secrets)
Push-Location $api
try {
  $secrets = (dotnet user-secrets list 2>$null) -join "`n"
  if ($secrets -notmatch 'Jwt:SecretKey') {
    $bytes = New-Object byte[] 48
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    dotnet user-secrets set 'Jwt:SecretKey' ([Convert]::ToBase64String($bytes)) | Out-Null
    Write-Host 'Đã sinh khóa JWT ngẫu nhiên.'
  }
  if ($secrets -notmatch 'Seed:DemoPassword') {
    do {
      $pwd1 = Read-Host 'Đặt mật khẩu cho các tài khoản demo (>= 8 ký tự, có hoa, thường, số)'
      $ok = $pwd1.Length -ge 8 -and $pwd1 -cmatch '[A-Z]' -and $pwd1 -cmatch '[a-z]' -and $pwd1 -match '\d'
      if (-not $ok) { Write-Host 'Mật khẩu chưa đủ mạnh, nhập lại.' -ForegroundColor Yellow }
    } until ($ok)
    dotnet user-secrets set 'Seed:DemoPassword' $pwd1 | Out-Null
    Write-Host 'Đã lưu mật khẩu demo bằng User Secrets.'
  }
} finally { Pop-Location }

# 3. Dữ liệu demo
$db = Join-Path $api 'eduviet-demo.db'
if ($ResetData -and (Test-Path $db)) {
  Remove-Item $db, "$db-shm", "$db-wal" -ErrorAction SilentlyContinue
  Write-Host 'Đã xóa DB demo, dữ liệu mẫu sẽ được tạo lại.'
}

# 4. Gói npm
if (-not (Test-Path (Join-Path $web 'node_modules'))) {
  Write-Host 'Đang cài gói npm (lần đầu, mất vài phút)...'
  Push-Location $web
  try { npm install } finally { Pop-Location }
}

# 5. Chạy
Start-Process powershell -WorkingDirectory $api -ArgumentList '-NoExit', '-Command', 'dotnet run --launch-profile demo-sqlite'
Start-Process powershell -WorkingDirectory $web -ArgumentList '-NoExit', '-Command', 'npm run dev'

Write-Host ''
Write-Host 'Đã mở hai cửa sổ API và web. Chờ khoảng 30 giây rồi mở:' -ForegroundColor Green
Write-Host '  Website : http://localhost:5173'
Write-Host '  Swagger : http://localhost:5080/swagger'
Write-Host 'Tài khoản demo: xem setup\test-data\README.md'
