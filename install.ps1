# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

if (-not ($IsWindows -or $PSVersionTable.PSEdition -eq "Desktop")) {
    Write-Error "gh-sync is currently only supported on Windows";
}

# Is gh-sync already on the path?
$ghSync = Get-Command gh-sync -ErrorAction SilentlyContinue;
if ($ghSync) {
    # Replace gh-sync.exe in place.
    $target = $ghSync.Source;
} else {
    # Make a new .gh-sync folder under ~ and add it to PATH.
    $ghBinPath = (Join-Path `
        ([System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::UserProfile)) `
        ".gh-sync" `
    );
    $ghBin = Get-Item $ghBinPath -ErrorAction SilentlyContinue;
    if (-not $ghBin) {
        $ghBin = New-Item $ghBinPath -ItemType Directory;
    }

    # Add to PATH.
    $userPath = [System.Environment]::GetEnvironmentVariable("PATH", [System.EnvironmentVariableTarget]::User);
    Write-Host "Adding to PATH. User PATH was:`n$userPath";
    $newPath = "$userPath;$ghBinPath";
    [System.Environment]::SetEnvironmentVariable("PATH", $newPath, [System.EnvironmentVariableTarget]::User);

    # Also set PATH temporarily.
    $Env:PATH = "$Env:PATH;$ghBinPath";

    # Finally, copy into new path.
    $target = $ghBin;
}
Push-Location (Join-Path $PSScriptRoot "src/GhSync");
dotnet publish --self-contained --runtime win10-x64 /p:PublishSingleFile=true
Copy-Item bin/Debug/net6.0-windows/win10-x64/publish/gh-sync.exe $target
Pop-Location;
