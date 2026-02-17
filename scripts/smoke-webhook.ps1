param(
  [string]$Url = "http://localhost:5287/webhook/swish",
  [string]$Secret = "dev_secret",
  [switch]$Replay
)

# 1) EXACT body string (change if you want, but keep it identical in both signing and sending)
$Body = '{"id":"smoke-verify","amount":50}'

# 2) Build ts/nonce + canonical
$Ts    = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$Nonce = [guid]::NewGuid().ToString()
$Canon = "$Ts`n$Nonce`n$Body"

# 3) HMAC (Base64)
$Hmac   = [System.Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($Secret))
$Raw    = $Hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($Canon))
$SigB64 = [Convert]::ToBase64String($Raw)

Write-Host "`n--- SMOKE ---"
Write-Host "TS      : $Ts"
Write-Host "NONCE   : $Nonce"
Write-Host "BODY    : $Body"
Write-Host "MSG     :`n$Canon"
Write-Host "SIG_B64 : $SigB64"
Write-Host "--------`n"

# 4) Send using EXACT bytes (not hashtable, not auto-JSON)
$Bytes = [Text.Encoding]::UTF8.GetBytes($Body)
try {
  $resp = Invoke-RestMethod -Method Post -Uri $Url `
    -Headers @{
      'X-Swish-Timestamp' = "$Ts"
      'X-Swish-Nonce'     = $Nonce
      'X-Swish-Signature' = $SigB64
    } `
    -Body $Bytes `
    -ContentType 'application/json; charset=utf-8'

  ($resp | ConvertTo-Json -Compress)
} catch {
  Write-Host $_.Exception.Message
  if ($_.Exception.Response -and $_.Exception.Response.GetResponseStream) {
    $sr = New-Object IO.StreamReader $_.Exception.Response.GetResponseStream()
    $errBody = $sr.ReadToEnd()
    Write-Host $errBody
  }
}

if ($Replay) {
  Write-Host "(replay) ---"
  try {
    # Send again with the SAME nonce/signature (should be rejected as replay: non-200, commonly 401/403/409)
    $resp2 = Invoke-RestMethod -Method Post -Uri $Url `
      -Headers @{
        'X-Swish-Timestamp' = "$Ts"
        'X-Swish-Nonce'     = $Nonce
        'X-Swish-Signature' = $SigB64
      } `
      -Body $Bytes `
      -ContentType 'application/json; charset=utf-8'
    ($resp2 | ConvertTo-Json -Compress)
  } catch {
    Write-Host $_.Exception.Message
    if ($_.Exception.Response -and $_.Exception.Response.GetResponseStream) {
      $sr2 = New-Object IO.StreamReader $_.Exception.Response.GetResponseStream()
      $errBody2 = $sr2.ReadToEnd()
      Write-Host $errBody2
    }
  }
}
