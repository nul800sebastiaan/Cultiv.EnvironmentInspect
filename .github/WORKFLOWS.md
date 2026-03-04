# GitHub Actions Local Testing with Act

This directory contains GitHub Actions workflows and local testing configuration.

## Running Workflows Locally with Act

[Act](https://github.com/nektos/act) allows you to run GitHub Actions workflows locally using Docker.

### Prerequisites

- **Docker Desktop** must be running
- **Git** for repository access
- **GitHub CLI** (`gh`) for authentication
- **Act** - either standalone or as a GitHub CLI extension

### Installing Act

Choose one of the following:

**Option 1: GitHub CLI Extension (Recommended)**
```powershell
gh extension install https://github.com/nektos/gh-act
```

**Option 2: Standalone**
```powershell
# Using winget
winget install nektos.act

# Using chocolatey
choco install act-cli
```

### Running the Workflow

From the repository root:
```powershell
.\.github\workflows\local\act.ps1
```

The script will automatically:
- Detect whether you're using standalone `act` or `gh act` extension
- Create missing `secrets.env` file if needed
- Use your GitHub token from `gh auth` for authentication
- Run the CI workflow in Docker containers

### Configuration Files

Located in `.github/workflows/local/`:

- **`act.ps1`** - Main script to run workflows locally
- **`vars.env`** - Environment variables (currently empty, add as needed)
- **`secrets.env`** - Secrets (auto-created if missing, add secrets in `KEY=value` format)
- **`payload.json`** - Event payload for triggering workflows

### What Gets Tested Locally

When running with act:
- ✅ **Setup job** - Version detection and semantic release
- ✅ **Build job** - Full build, Umbraco startup, code generation, and NuGet packaging
- ⏭️ **Windows SQL Server job** - Skipped (Windows containers not supported in act)
- ⏭️ **Security scan job** - Skipped (requires GitHub Security tab)

### Troubleshooting

**Docker not running:**
```
Error: Cannot connect to the Docker daemon
```
Solution: Start Docker Desktop

**Port already in use:**
```
level=fatal msg="listen tcp 10.6.12.2:34567: bind: Only one usage..."
```
Solution: Kill existing act process
```powershell
Stop-Process -Name gh-act -Force
```

**Act not found:**
```
Neither 'act' command nor 'gh act' extension found
```
Solution: Install act using one of the methods above

### Key Features

- **Automatic tool detection** - Works with both standalone act and gh extension
- **Health checking** - Waits for Umbraco to be ready before running code generation
- **Intelligent skipping** - Automatically skips Windows and security jobs that don't work in act
- **Artifact support** - Artifacts are stored in `%TEMP%\act-artifacts`

### Workflow Files

- **`ci.yml`** - Main CI/CD workflow for builds, tests, and packaging
- **`release.yml`** - Release workflow (not typically run locally)

For more information about act, visit: https://github.com/nektos/act
