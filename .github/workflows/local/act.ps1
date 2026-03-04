# Change to repository root (3 levels up from this script)
Push-Location (Join-Path $PSScriptRoot '..\..\..') -ErrorAction Stop

try {
    # Check if act is available
    $useGhAct = $false
    if (Get-Command act -ErrorAction SilentlyContinue) {
        Write-Host "Using standalone act command"
        $useGhAct = $false
    } elseif (Get-Command gh -ErrorAction SilentlyContinue) {
        # Check if gh act extension is installed
        $ghExtensions = gh extension list 2>$null
        if ($ghExtensions -match 'gh-act') {
            Write-Host "Using gh act extension"
            $useGhAct = $true
        } else {
            Write-Error "Neither 'act' command nor 'gh act' extension found. Please install one of them."
            Write-Host "Install options:"
            Write-Host "  - Standalone: https://github.com/nektos/act"
            Write-Host "  - GH Extension: gh extension install https://github.com/nektos/gh-act"
            exit 1
        }
    } else {
        Write-Error "Neither 'act' command nor 'gh act' extension found. Please install one of them."
        Write-Host "Install options:"
        Write-Host "  - Standalone: https://github.com/nektos/act"
        Write-Host "  - GH Extension: gh extension install https://github.com/nektos/gh-act"
        exit 1
    }
    
    # Ensure secrets.env exists
    $secretsFile = Join-Path $PSScriptRoot 'secrets.env'
    if (-not (Test-Path $secretsFile)) {
        Write-Host "Creating empty secrets.env file..."
        New-Item -Path $secretsFile -ItemType File -Force | Out-Null
    }
    
    # Build and execute the act command
    $actArgs = @(
        'push'
        '-W', '.github/workflows/ci.yml'
        '--var-file', '.github/workflows/local/vars.env'
        '--secret-file', '.github/workflows/local/secrets.env'
        '-s', "GITHUB_TOKEN=$(gh auth token)"
        '--artifact-server-path', "$env:TEMP\act-artifacts"
        '-e', '.github/workflows/local/payload.json'
    )
    
    if ($useGhAct) {
        gh act @actArgs
    } else {
        act @actArgs
    }
}
finally {
    Pop-Location
}