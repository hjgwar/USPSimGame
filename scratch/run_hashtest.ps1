$dll = (Get-ChildItem -Path "bin\Debug\net10.0\USPSimGame.dll").FullName
[System.Reflection.Assembly]::LoadFrom($dll)

$hasher = New-Object USPSimGame.Services.PasswordHasherService
$oldHash = "AQAAAAIAAYagAAAAEIVLrf9vVvuAEbbpsNWq/ML0j+64yoIHPsiVx44+ssNWj0UgbRJKwTB+8iL+DFMwWg=="

$newHash = $hasher.HashPassword("secrete")
$resNew = $hasher.VerifyPassword($newHash, "secrete")
$resOldSecrete = $hasher.VerifyPassword($oldHash, "secrete")
$resOldSecret = $hasher.VerifyPassword($oldHash, "secret")

Write-Host "NEW_HASH: $newHash"
Write-Host "VERIFY_NEW_WITH_SECRETE: $resNew"
Write-Host "VERIFY_OLD_WITH_SECRETE: $resOldSecrete"
Write-Host "VERIFY_OLD_WITH_SECRET: $resOldSecret"
