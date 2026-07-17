[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$httpClient = $null
$content = $null
$token = $null

try {
    $token = [Environment]::GetEnvironmentVariable('TELEGRAM_BOT_TOKEN', 'User')
    $chatId = [Environment]::GetEnvironmentVariable('TELEGRAM_CHAT_ID', 'User')

    if ([string]::IsNullOrWhiteSpace($token)) {
        throw 'User environment variable TELEGRAM_BOT_TOKEN is not set.'
    }
    if ([string]::IsNullOrWhiteSpace($chatId)) {
        throw 'User environment variable TELEGRAM_CHAT_ID is not set.'
    }

    Add-Type -AssemblyName System.Net.Http
    $httpClient = New-Object System.Net.Http.HttpClient
    $testMessage = '"\u041f\u0440\u043e\u0432\u0435\u0440\u043a\u0430 Telegram API: \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0438\u0435 \u0440\u0430\u0431\u043e\u0442\u0430\u0435\u0442"' | ConvertFrom-Json
    $requestBody = 'chat_id={0}&text={1}' -f [Uri]::EscapeDataString($chatId), [Uri]::EscapeDataString($testMessage)
    $content = New-Object System.Net.Http.StringContent($requestBody, [Text.Encoding]::UTF8, 'application/x-www-form-urlencoded')

    $requestUri = 'https://api.telegram.org/bot{0}/sendMessage' -f $token
    $response = $httpClient.PostAsync($requestUri, $content).GetAwaiter().GetResult()
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
        throw 'Telegram API did not confirm successful message delivery.'
    }

    Write-Output 'Test message sent successfully.'
    exit 0
}
catch {
    $message = $_.Exception.Message
    if (-not [string]::IsNullOrEmpty($token)) {
        $message = $message.Replace($token, '[REDACTED]')
    }
    [Console]::Error.WriteLine("Telegram test failed: $message")
    exit 1
}
finally {
    if ($content) { $content.Dispose() }
    if ($httpClient) { $httpClient.Dispose() }
}
