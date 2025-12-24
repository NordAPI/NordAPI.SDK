param(
  [string]$Url = "http://localhost:5287/webhook/swish",
  [string]$Secret,
  [string]$Body = '{"event":"payment.created","id":"abc123","amount":100,"currency":"SEK"}',
  [string]$Nonce,
  [int]$Timestamp = 0
)

# 1) Resolve secret
if (-not $Secret -or $Secret.Trim() -eq "") {
  if ($env:SWISH_WEBHOOK_SECRET) { $Secret = $env:SWISH_WEBHOOK_SECRET }
  else {
    Write-Error "No secret provided. Pass -Secret '...' or set SWISH_WEBHOOK_SECRET."
    exit 1
  }
}

# 2) Timestamp & nonce
if ($Timestamp -eq 0) { $Timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() }
if (-not $Nonce -or $Nonce.Trim() -eq "") { $Nonce = [guid]::NewGuid().ToString("N") }

# 3) Canonical + signature (HMAC-SHA256, Base64)
$canonical       = "$Timestamp`n$Nonce`n$Body"
$secretBytes     = [Text.Encoding]::UTF8.GetBytes($Secret)
$canonicalBytes  = [Text.Encoding]::UTF8.GetBytes($canonical)
$hmac            = [System.Security.Cryptography.HMACSHA256]::new($secretBytes)
$sigBytes        = $hmac.ComputeHash($canonicalBytes)
$signature       = [Convert]::ToBase64String($sigBytes)

# 4) Headers
$headers = @{
  "X-Swish-Timestamp" = "$Timestamp"
  "X-Swish-Signature" = "$signature"
  "X-Swish-Nonce"     = "$Nonce"
}

Write-Host "→ POST $Url"
Write-Host "  Timestamp: $Timestamp"
Write-Host "  Nonce    : $Nonce"
Write-Host "  Signature: $signature"
Write-Host "  Body     : $Body"

# 5) POST
try {
  $resp = Invoke-RestMethod -Method Post -Uri $Url -Headers $headers -Body $Body -ContentType "application/json"
  Write-Host "✔ 200 OK"
  $resp | ConvertTo-Json -Depth 10
} catch {
  $wex = $_.Exception
  if ($wex.Response) {
    $status = [int]$wex.Response.StatusCode
    $reader = New-Object System.IO.StreamReader($wex.Response.GetResponseStream())
    $text   = $reader.ReadToEnd()
    Write-Host "✖ $status"
    if ($text) { Write-Host $text }
    switch ($status) {
      401 { Write-Host "Hint: Server and client must use the exact same SWISH_WEBHOOK_SECRET." }
      409 { Write-Host "Hint: Replay protection — retry with a new -Nonce (or let the script generate one)." }
      400 { Write-Host "Hint: Verify that headers and body are sent correctly." }
      500 { Write-Host "Hint: Set the secret in the server terminal before starting: ``$env:SWISH_WEBHOOK_SECRET='local-webhook-secret'`` and restart the app." }
    }
  } else {
    Write-Host "✖ Error: $($wex.Message)"
  }
  exit 1
}
