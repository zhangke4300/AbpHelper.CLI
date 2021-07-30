. ".\common.ps1"

# Rebuild solution
Set-Location $rootFolder
& dotnet build -c Release
Write-Host $rootFolder
if (-Not $?) {
	Write-Host ("Building failed.")
	exit $LASTEXITCODE
}
    
# Copy nuget package
$projectPackPath = Join-Path $rootFolder ("/src/AbpHelper/bin/Release/EasyAbp.AbpHelper.*.nupkg")
Write-Host $projectPackPath
Move-Item $projectPackPath $packFolder

# Go back to the pack folder
Set-Location $packFolder