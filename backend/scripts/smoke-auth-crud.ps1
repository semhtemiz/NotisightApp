param(
    [string]$BaseUrl = "http://127.0.0.1:5047"
)

$ErrorActionPreference = "Stop"

$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$email = "smoke-$suffix@example.com"
$password = "Smoke123!"

Write-Host "Registering test user: $email"
$registerBody = @{
    displayName = "Smoke User"
    email = $email
    password = $password
} | ConvertTo-Json

$registerResponse = Invoke-RestMethod -Method Post -Uri "$BaseUrl/auth/register" -ContentType "application/json" -Body $registerBody
$accessToken = $registerResponse.accessToken
$headers = @{
    Authorization = "Bearer $accessToken"
}

Write-Host "Creating tag"
$tagBody = @{
    name = "smoke-tag"
} | ConvertTo-Json
$tagResponse = Invoke-RestMethod -Method Post -Uri "$BaseUrl/tags" -Headers $headers -ContentType "application/json" -Body $tagBody

Write-Host "Creating folder"
$folderBody = @{
    name = "Smoke Folder"
    parentFolderId = $null
} | ConvertTo-Json
$folderResponse = Invoke-RestMethod -Method Post -Uri "$BaseUrl/folders" -Headers $headers -ContentType "application/json" -Body $folderBody

Write-Host "Creating note"
$noteBody = @{
    title = "Smoke Note"
    content = "Smoke test content"
    folderId = $folderResponse.id
    tagIds = @($tagResponse.id)
} | ConvertTo-Json
$noteResponse = Invoke-RestMethod -Method Post -Uri "$BaseUrl/notes" -Headers $headers -ContentType "application/json" -Body $noteBody

Write-Host "Listing notes"
$notes = Invoke-RestMethod -Method Get -Uri "$BaseUrl/notes" -Headers $headers

Write-Host "Fetching note by id"
$noteById = Invoke-RestMethod -Method Get -Uri "$BaseUrl/notes/$($noteResponse.id)" -Headers $headers

Write-Host "Deleting note, folder, and tag"
Invoke-RestMethod -Method Delete -Uri "$BaseUrl/notes/$($noteResponse.id)" -Headers $headers | Out-Null
Invoke-RestMethod -Method Delete -Uri "$BaseUrl/folders/$($folderResponse.id)" -Headers $headers | Out-Null
Invoke-RestMethod -Method Delete -Uri "$BaseUrl/tags/$($tagResponse.id)" -Headers $headers | Out-Null

Write-Host ""
Write-Host "Smoke test completed successfully."
Write-Host "Registered user id: $($registerResponse.user.id)"
Write-Host "Created note id: $($noteById.id)"
Write-Host "Total notes returned during list: $($notes.Count)"
