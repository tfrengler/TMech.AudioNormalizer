$BuildDir = "$PSScriptRoot/Build/bin/TMech.AudioNormalizer/net9.0/"

& "$PSScriptRoot/build.ps1" "Release" "win-x64"
Copy-Item "$BuildDir/win-x64/publish/TMech.AudioNormalizer.exe" -Destination "$PSScriptRoot/Build/TMech.AudioNormalizer.exe"

& "$PSScriptRoot/build.ps1" "Release" "linux-x64"
Copy-Item "$BuildDir/linux-x64/publish/TMech.AudioNormalizer" -Destination "$PSScriptRoot/Build/TMech.AudioNormalizer"

Write-Host -f Green "ALL DONE"