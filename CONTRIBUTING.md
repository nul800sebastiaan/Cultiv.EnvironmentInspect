# Contributing to Cultiv.EnvironmentInspect

Thank you for your interest in contributing to Cultiv Environment Inspector! This document provides guidelines and information for contributors.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Branching Strategy](#branching-strategy)
- [Commit Messages](#commit-messages)
- [Pull Request Process](#pull-request-process)
- [Testing](#testing)
- [Release Process](#release-process)
- [Documentation](#documentation)

## Getting Started

Before you begin:

1. Check [existing issues](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/issues) to see if your bug/feature is already being discussed
2. For new features, open an issue first to discuss the approach before investing time in implementation
3. Fork the repository and create a branch from `develop/v2`

## Development Setup

### Prerequisites

- .NET 10.0 SDK
- Node.js LTS (for frontend development)
- An IDE (Visual Studio, VS Code, or Rider recommended)
- SQL Server or SQLite (for local database)

### Local Development

1. **Clone the repository:**
   ```bash
   git clone https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect.git
   cd Cultiv.EnvironmentInspect
   ```

2. **Build the solution:**
   ```bash
   dotnet build
   ```

3. **Run the demo site:**
   ```bash
   cd Cultiv.EnvironmentInspect.DemoSite
   dotnet run
   ```

4. **Generate TypeScript API client (codegen):**
   
   With the demo site running, open a new terminal and generate the TypeScript client from the OpenAPI/Swagger endpoint:
   ```bash
   cd Cultiv.EnvironmentInspect/Client
   npm install
   npm run generate-client
   ```
   
   This generates type-safe TypeScript client code in `src/api/` from the backend API endpoints.

5. **Build the frontend:**
   ```bash
   # Still in Cultiv.EnvironmentInspect/Client
   npm run build
   cd ../..
   ```

5. **Access the dashboard:**
   - Open your browser to the configured URL (check console output)
   - Navigate to **Settings → Environment Inspector**

### Frontend Development

For frontend development with hot reload:

```bash
cd Cultiv.EnvironmentInspect/Client
npm run watch
```

This starts Vite in watch mode. The frontend will rebuild automatically on file changes.

**Note**: If you modify backend API endpoints, you'll need to regenerate the TypeScript client:
```bash
# Ensure demo site is running, then:
npm run generate-client
```

### Database Migrations

If you modify the `UserPreference` entity or add new entities:

```bash
cd Cultiv.EnvironmentInspect
dotnet ef migrations add MigrationName --startup-project ../Cultiv.EnvironmentInspect.DemoSite
```

⚠️ **Never modify migrations after they've been committed and released.**

## Coding Standards

This project follows strict coding standards to maintain consistency. See [`.github/copilot-instructions.md`](.github/copilot-instructions.md) for detailed guidelines.

### Key Standards

**C# Code:**
- Use 4-space indentation (not tabs)
- Line endings: CRLF (Windows)
- Use dependency injection for services
- Log important operations with `ILogger<T>`
- Handle errors gracefully with try-catch and logging
- Use nullable reference types (`#nullable enable`)
- Use implicit usings (configured in project)

**TypeScript/Frontend:**
- Follow ESLint rules (configured in `Client/eslint.config.js`)
- Use TypeScript strict mode
- Use Lit for web components (Umbraco backoffice framework)

**Formatting:**
```bash
# Format all C# code (REQUIRED before committing)
dotnet format

# Verify formatting (used in CI)
dotnet format --verify-no-changes
```

⚠️ **All PRs must pass `dotnet format --verify-no-changes` in CI or they will be rejected.**

### Anti-Patterns to Avoid

❌ Don't commit code that fails `dotnet format`  
❌ Don't use real credentials or organization names in examples/docs  
❌ Don't add features without updating documentation  
❌ Don't use tabs for indentation  
❌ Don't ignore logger warnings  
❌ Don't modify migrations after they've been committed  

## Branching Strategy

This project uses Git Flow with version-specific branches:

### Main Branches

- **`develop/v2`** - Main development branch for v2.x (protected, default branch)
- **`release/v2`** - Stable release branch for production releases (protected)
- **`beta/v2`** - Beta releases (protected)

### Supporting Branches

- **`feature/v2/<feature-name>`** - New features
  - Branch from: `develop/v2`
  - Merge back into: `develop/v2`
  - Example: `feature/v2/add-export-to-csv`

- **`hotfix/v2/<fix-name>`** - Urgent production fixes
  - Branch from: `release/v2`
  - Merge back into: `release/v2` and `develop/v2`
  - Example: `hotfix/v2/fix-memory-leak`

### Branching Workflow

```bash
# Create a feature branch
git checkout develop/v2
git pull origin develop/v2
git checkout -b feature/v2/my-new-feature

# Work on your feature, commit regularly
git add .
git commit -m "feat: add new feature"

# Push your branch
git push -u origin feature/v2/my-new-feature

# Create a pull request to develop/v2
```

### Umbraco 17/18 branch lines (v2/v3) and git worktrees

This repo supports two Umbraco CMS majors from parallel branch lines:

- `develop/v2` / `release/v2` — targets Umbraco CMS 17. **All new feature and bugfix development happens here** (everything above in this section applies to it).
- `develop/v3` / `release/v3` — targets Umbraco CMS 18. This line is merge-only — never branch a feature or hotfix directly off it; changes only arrive there via `git merge develop/v2` (never `git cherry-pick`, so the same change doesn't get re-flagged as a conflict on every later merge).

Switching between `develop/v2` and `develop/v3` with a plain `git checkout` in one working copy leaves stale artifacts behind: `node_modules` installed against the wrong `@umbraco-cms/backoffice` version, `bin`/`obj` build output from the other major, and a local runtime database migrated by the wrong Umbraco major (which then refuses to boot at all). A [git worktree](https://git-scm.com/docs/git-worktree) avoids this entirely — it gives `develop/v3` its own working directory and build artifacts while still sharing the same repository history as your existing clone, so nothing needs re-downloading.

Run this from inside your existing clone (with `develop/v2` checked out):

```bash
# <path> can be any folder of your choosing that doesn't already exist - a sibling directory
# next to your existing clone is the usual convention, e.g. ../Cultiv.EnvironmentInspect-v3
git worktree add <path> develop/v3
```

`cd` into `<path>` to work on `develop/v3` from then on — its own `npm install`, `dotnet build`, and local database stay completely independent of your `develop/v2` working copy, so you can have both branches built and running at the same time without either interfering with the other.

Useful commands:
```bash
git worktree list           # see every worktree and which branch it has checked out
git worktree remove <path>  # remove one you no longer need (must be clean - commit or stash first)
```

See [`.github/copilot-instructions.md`](.github/copilot-instructions.md)'s "Two Umbraco majors, one repo" section for the full v2→v3 merge workflow, including which files are deliberately kept different between the two branches (`.gitattributes` `merge=ours` entries) and the post-merge checklist.

## Commit Messages

This project uses [Conventional Commits](https://www.conventionalcommits.org/) for automated versioning and changelog generation via semantic-release.

### Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- **`feat:`** - New feature (triggers minor version bump)
- **`fix:`** - Bug fix (triggers patch version bump)
- **`docs:`** - Documentation changes only
- **`style:`** - Code style changes (formatting, no functional changes)
- **`refactor:`** - Code refactoring (no feature changes or bug fixes)
- **`perf:`** - Performance improvements
- **`test:`** - Adding or updating tests
- **`chore:`** - Maintenance tasks, dependency updates
- **`ci:`** - CI/CD configuration changes
- **`build:`** - Build system changes

### Breaking Changes

For breaking changes, add `BREAKING CHANGE:` in the commit body or footer, or use `!` after the type:

```
feat!: remove support for Umbraco v13

BREAKING CHANGE: This version only supports Umbraco v17+
```

This triggers a major version bump.

### Examples

```bash
# Feature
git commit -m "feat: add CSV export functionality"

# Bug fix
git commit -m "fix: resolve memory leak in cache handler"

# Documentation
git commit -m "docs: update configuration examples in CONFIGURATION.md"

# Breaking change
git commit -m "feat!: migrate to .NET 10

BREAKING CHANGE: Requires .NET 10 SDK or higher"
```

## Pull Request Process

1. **Ensure your code builds and tests pass:**
   ```bash
   dotnet build
   dotnet format --verify-no-changes
   ```

2. **Update documentation:**
   - Update `README.md` if user-facing features changed
   - Update `CONFIGURATION.md` if configuration options changed
   - Update `.github/copilot-instructions.md` if coding patterns changed
   - Add XML documentation comments to public APIs

3. **Create a pull request:**
   - Target: `develop/v2` for features, `release/v2` for hotfixes
   - Title: Use conventional commit format (e.g., `feat: add export to CSV`)
   - Description: Explain what changed and why
   - Reference related issues with `Fixes #123` or `Closes #456`

4. **Code review:**
   - Address reviewer feedback
   - Keep commits clean and logical
   - Rebase on target branch if needed

5. **Merge:**
   - PRs are squash-merged to keep history clean
   - Ensure the squashed commit message follows conventional commits format

## Testing

### Manual Testing

1. Run the demo site
2. Test your changes in the Environment Inspector dashboard
3. Verify hot reload functionality (if applicable)
4. Test with different Umbraco configurations
5. Test with both SQLite and SQL Server databases (if relevant)

### Test Configuration Scenarios

When testing configuration changes, verify:

- Different redaction modes (Full, Partial, Advanced)
- Exclusion patterns (regex matching)
- Provider filtering
- Starred favorites persistence
- Search and filter functionality
- Copy to clipboard features
- Azure Web App JSON export (if enabled)

### Automated Tests

The project uses GitHub Actions CI/CD with automated testing:

**Linux Build (SQLite):**
- Runs on Ubuntu with SQLite database
- Builds the solution and frontend
- Starts Umbraco demo site and validates it responds
- Generates TypeScript client via OpenAPI/Swagger
- Checks database migrations completed successfully
- Validates NuGet package structure with `dotnet-validate`
- Packs and uploads NuGet package for downstream testing

**Windows Test (SQL Server):**
- Runs on Windows with SQL Server LocalDB
- Downloads the NuGet package from the Linux build
- Installs the package in a fresh demo site (replacing project reference)
- Validates the package installs correctly via NuGet
- Starts Umbraco with SQL Server LocalDB
- Checks database migrations work correctly with SQL Server
- Confirms both Umbraco and Cultiv.EnvironmentInspect migrations succeed

**Migration Validation:**

Both jobs use `.github/scripts/Check-MigrationErrors.ps1` to verify:
- `Unattended install completed` (Umbraco database setup)
- `Cultiv Environment Inspect migrations completed successfully` (package migrations)
- No `[ERR]` or `[FTL]` log entries after installation starts
- No `PendingModelChanges` errors

**Security Scanning:**
- Trivy vulnerability scanner runs on all builds

### Contributing Automated Tests

Currently, the project relies on manual testing for frontend functionality. Contributions to add automated tests are welcome! Areas that would benefit:

- Unit tests for redaction logic
- Integration tests for API endpoints
- End-to-end tests for dashboard functionality

## Release Process

Releases are automated using semantic-release based on conventional commits.

### Version Bumping

- **Major** (X.0.0): Breaking changes (`BREAKING CHANGE:` in commit or `!` after type)
- **Minor** (0.X.0): New features (`feat:` commits)
- **Patch** (0.0.X): Bug fixes (`fix:` commits)

### Release Workflow

1. **Merge to `develop/v2`**: All features and fixes go here first
2. **CI runs automatically**: Builds, formats check, generates codegen, generates preview version
3. **Merge to `release/v2`**: When ready to release, merge develop to release branch
4. **Manual release trigger**: Maintainers trigger the release workflow via GitHub Actions
5. **Semantic release analyzes commits**: Determines version bump based on conventional commits
6. **Automated steps:**
   - Creates git tag
   - Generates changelog and GitHub release notes
   - Builds NuGet package
   - Publishes to nuget.org (if enabled)

### Creating a Release (Maintainers Only)

⚠️ **Full releases MUST be run from the `release/v2` branch.**  
⚠️ **Pre-releases are generated by running the workflow from the `develop/v2` branch.**

**For Pre-releases (from develop/v2):**

1. Ensure all desired changes are merged to `develop/v2`
2. Navigate to **Actions → Build and Package**
3. Click **Run workflow**
4. Select branch: **`develop/v2`**
5. Check options:
   - ✅ **Create GitHub Release** (will be marked as pre-release)
   - ⬜ **Publish to nuget.org** (optional - usually unchecked for pre-releases)
6. Click **Run workflow**

**For Full/Stable Releases (from release/v2):**

1. Ensure all desired changes are merged to `develop/v2`
2. Merge `develop/v2` into `release/v2`
3. Navigate to **Actions → Build and Package**
4. Click **Run workflow**
5. Select branch: **`release/v2`**
6. Check options:
   - ✅ **Create GitHub Release**
   - ✅ **Publish to nuget.org**
7. Click **Run workflow**

The workflow will fail if there are no conventional commits since the last release.

## Documentation

### When to Update Documentation

- **`README.md`**: User-facing features, installation, basic usage
- **`CONFIGURATION.md`**: Configuration options, redaction modes, examples
- **`.github/copilot-instructions.md`**: Coding patterns, development workflows
- **`CONTRIBUTING.md`** (this file): Contribution process, branching, releases

### Documentation Standards

- Use clear, concise language
- Include code examples for complex topics
- Use fake data in examples (not real credentials or organizations)
- Keep examples up to date with code changes
- Link between related documentation sections

### Example Pattern from CONFIGURATION.md

When documenting configuration options:

````markdown
*Option Name: Brief description*
```json
{
  "Key": "pattern",
  "RedactionMode": "Mode",
  "RedactionOptions": { /* options */ }
}
```
Input: `example input string`  
Result: `example output with redaction`
````

## Getting Help

- **Issues**: [GitHub Issues](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/issues)
- **Discussions**: Open an issue for questions or feature discussions
- **Documentation**: Check `README.md`, `CONFIGURATION.md`, `.github/copilot-instructions.md`

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to Cultiv.EnvironmentInspect! 🎉
