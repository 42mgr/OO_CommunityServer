# Create a file with your changed .cs files
# Then use MSBuild to find affected projects

# PowerShell script
$changedFiles = Get-Content "modified-files.txt"
$affectedProjects = @()

foreach ($file in $changedFiles) {
    # Find which project contains this file
    $projects = Get-ChildItem -Recurse -Filter "*.csproj" | 
        Where-Object { 
            (Get-Content $_.FullName -Raw) -match [regex]::Escape($file)
        }
    
    $affectedProjects += $projects
}

# Get unique DLL names
$affectedProjects | Select-Object -Unique | ForEach-Object {
    $projectContent = Get-Content $_.FullName -Raw
    if ($projectContent -match '<AssemblyName>(.*?)</AssemblyName>') {
        Write-Output "$($matches[1]).dll"
    } else {
        Write-Output "$($_.BaseName).dll"
    }
}
