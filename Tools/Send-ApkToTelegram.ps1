[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ApkPath,

    [Parameter(Mandatory = $false)]
    [string]$Caption
)

$ErrorActionPreference = 'Stop'

function Get-UserEnvironmentVariable {
    param([Parameter(Mandatory = $true)][string]$Name)

    return [Environment]::GetEnvironmentVariable($Name, 'User')
}

function Get-SafeErrorMessage {
    param(
        [Parameter(Mandatory = $true)][System.Exception]$Exception,
        [AllowEmptyString()][string]$Secret
    )

    $message = $Exception.Message
    if (-not [string]::IsNullOrEmpty($Secret)) {
        $message = $message.Replace($Secret, '[REDACTED]')
    }
    return $message
}

$httpClient = $null
$multipart = $null
$fileStream = $null

try {
    $token = Get-UserEnvironmentVariable -Name 'TELEGRAM_BOT_TOKEN'
    $chatId = Get-UserEnvironmentVariable -Name 'TELEGRAM_CHAT_ID'

    if ([string]::IsNullOrWhiteSpace($token)) {
        throw 'User environment variable TELEGRAM_BOT_TOKEN is not set.'
    }
    if ([string]::IsNullOrWhiteSpace($chatId)) {
        throw 'User environment variable TELEGRAM_CHAT_ID is not set.'
    }
    if (-not (Test-Path -LiteralPath $ApkPath -PathType Leaf)) {
        throw "File not found: $ApkPath"
    }

    $apk = Get-Item -LiteralPath $ApkPath
    if (-not [string]::Equals($apk.Extension, '.apk', [StringComparison]::OrdinalIgnoreCase)) {
        throw "File must have the .apk extension: $($apk.FullName)"
    }
    if ($apk.Length -le 0) {
        throw "APK file is empty: $($apk.FullName)"
    }

    Add-Type -AssemblyName System.Net.Http
    $httpClient = New-Object System.Net.Http.HttpClient
    $multipart = New-Object System.Net.Http.MultipartFormDataContent

    $multipart.Add((New-Object System.Net.Http.StringContent($chatId)), 'chat_id')
    if (-not [string]::IsNullOrWhiteSpace($Caption)) {
        $multipart.Add((New-Object System.Net.Http.StringContent($Caption, [Text.Encoding]::UTF8)), 'caption')
    }

    $fileStream = [IO.File]::OpenRead($apk.FullName)
    $fileContent = New-Object System.Net.Http.StreamContent($fileStream)
    $fileContent.Headers.ContentType = New-Object System.Net.Http.Headers.MediaTypeHeaderValue('application/vnd.android.package-archive')
    $multipart.Add($fileContent, 'document', $apk.Name)

    $requestUri = 'https://api.telegram.org/bot{0}/sendDocument' -f $token
    $response = $httpClient.PostAsync($requestUri, $multipart).GetAwaiter().GetResult()
    $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    if (-not $response.IsSuccessStatusCode) {
        $description = $null
        try { $description = ($responseBody | ConvertFrom-Json).description } catch { }
        if ([string]::IsNullOrWhiteSpace($description)) {
            $description = 'Telegram API returned no error description.'
        }
        throw "Telegram API returned HTTP $([int]$response.StatusCode): $description"
    }

    $result = $responseBody | ConvertFrom-Json
    if (-not $result.ok) {
        throw 'Telegram API did not confirm successful APK delivery.'
    }

    Write-Output "APK sent successfully: $($apk.FullName)"
    exit 0
}
catch {
    $safeMessage = Get-SafeErrorMessage -Exception $_.Exception -Secret $token
    [Console]::Error.WriteLine("APK delivery failed: $safeMessage")
    exit 1
}
finally {
    if ($multipart) { $multipart.Dispose() }
    if ($fileStream) { $fileStream.Dispose() }
    if ($httpClient) { $httpClient.Dispose() }
}
