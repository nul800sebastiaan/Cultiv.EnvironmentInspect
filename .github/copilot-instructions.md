# Cultiv.EnvironmentInspect Development Guidelines

## Cross-Branch Workflow (v2 → v3) — Check This Every Time

**Whenever you commit and push a change to `develop/v2`, before considering the task complete: stop and decide whether it also belongs on `develop/v3`.** The default answer is yes — almost all business logic is shared (see "Two Umbraco majors, one repo" below for the full merge workflow and the narrow exceptions, e.g. a fix that's genuinely Swashbuckle/Umbraco-17-only). Concretely: `cd` into the `../Cultiv.EnvironmentInspect-v3` worktree, `git merge develop/v2` (never `git cherry-pick` — it creates independent commits with no shared ancestry, so the same logical change gets re-flagged as a fresh conflict on every later merge), rebuild the Client if the merge touched `Client/src/**`, push `develop/v3`, wait for CI to go green, then fast-forward and push `release/v3` to match.

## Project Overview

Cultiv Environment Inspector is an Umbraco CMS package (v17+) that provides a dashboard in the Settings section showing all configuration values and their sources (appsettings.json, environment variables, Azure Key Vault, etc.).

**Key Technologies:**
- **Backend**: ASP.NET Core (net10.0), Umbraco CMS v17+, EF Core
- **Frontend**: TypeScript, Lit (web components), Vite (in `/Client` folder)
- **Database**: SQLite and SQL Server supported (migrations via EF Core)
- **Build**: GitHub Actions CI/CD, NuGet package publishing

## Project Structure

```
Cultiv.EnvironmentInspect/           # Main package
  ├── Client/                         # Frontend (TypeScript/Vue/Vite)
  ├── Services/                       # Core business logic
  │   └── EnvironmentInspectService.cs
  ├── Controllers/                    # Web API endpoints
  ├── Data/                           # EF Core DbContext and entities
  ├── Migrations/                     # EF Core migrations
  ├── Configuration/                  # Options classes
  ├── Composers/                      # Umbraco composers
  └── wwwroot/                        # Built frontend assets

Cultiv.EnvironmentInspect.DemoSite/  # Test site for local development
```

## Coding Standards

### C# Code

**Formatting:**
- Run `dotnet format` after making changes
- Use 4-space indentation (not tabs)
- Line endings: CRLF (Windows)
- Follow existing code patterns in the file you're editing

**Patterns to Follow:**
- Use dependency injection for services
- Log important operations with `ILogger<T>`
- Handle errors gracefully with try-catch and logging
- Use nullable reference types (`#nullable enable`)
- Use implicit usings (configured in project)
- Use `IOptionsMonitor<T>` (not `IOptions<T>`) for reactive configuration that responds to hot reload
- Gracefully degrade on validation errors (e.g., invalid regex patterns) - log warnings and continue execution

**Example Service Pattern:**
```csharp
public class MyService : IMyService
{
    private readonly ILogger<MyService> _logger;
    private readonly IConfiguration _configuration;

    public MyService(
        ILogger<MyService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public string ProcessData(string input)
    {
        try
        {
            // Implementation
            _logger.LogInformation("Successfully processed data");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing data: {Input}", input);
            throw;
        }
    }
}
```

### TypeScript/Frontend

The frontend is in the `Client/` folder and uses:
- TypeScript with strict mode
- Lit for web components (Umbraco backoffice framework)
- Vite for building
- ESLint for linting

**Development workflow:**
```bash
cd Cultiv.EnvironmentInspect/Client

# Generate TypeScript API client from OpenAPI (needs demo site running)
npm run generate-client

# Build for production
npm run build

# Watch mode for development (auto-rebuild on changes)
npm run watch
```

**Important**: If you modify backend API endpoints, regenerate the TypeScript client with `npm run generate-client` while the demo site is running.

## Configuration & Documentation

### Adding New Features

When adding new redaction modes, configuration options, or features:

1. **Update the code** in the appropriate service/controller
2. **Update `CONFIGURATION.md`** with:
   - New configuration options in the reference table
   - Example JSON configuration snippets
   - Input/output examples showing the feature in action
3. **Update `README.md`** if it's a user-facing feature
4. **Consider adding to `appsettings-schema.Cultiv.EnvironmentInspect.json`** for IntelliSense

### Configuration Documentation Pattern

When documenting redaction/exclusion rules in `CONFIGURATION.md`:
```markdown
*Option X: Description of the feature*
```json
{
  "Key": "pattern",
  "RedactionMode": "Mode",
  "RedactionOptions": { /* options */ }
}
```
Input: `example input string`  
Result: `example output with redaction`
```

**Use fake data in examples:**
- Use `example-server.database.windows.net` for servers
- Use `S3cr3tP@ssw0rd!XyZ` style for passwords
- Use random GUIDs like `a1b2c3d4-5678-9abc-def0-123456789abc`
- Avoid real organization names or credentials

## Building & Testing

### Build Commands

```bash
# Build the entire solution
dotnet build

# Build just the package
dotnet build Cultiv.EnvironmentInspect/Cultiv.EnvironmentInspect.csproj

# Format code (required before committing)
dotnet format

# Verify formatting
dotnet format --verify-no-changes

# Run the demo site
cd Cultiv.EnvironmentInspect.DemoSite
dotnet run
```

### Running Locally

1. Build solution: `dotnet build`
2. Start the demo site: `dotnet run` in `Cultiv.EnvironmentInspect.DemoSite/`
3. Generate TypeScript client (in new terminal): `cd Cultiv.EnvironmentInspect/Client && npm run generate-client`
4. Build frontend: `npm run build` (or `npm run watch` for development)
5. Access Umbraco at `https://localhost:44310` (or configured port in launchSettings.json)
6. Navigate to **Settings → Environment Inspector**

### Testing

⚠️ **No automated testing framework is configured.** All features must be manually tested using the demo site.

**Manual testing workflow:**
1. Make your code changes
2. Rebuild: `dotnet build`
3. If you modified API endpoints, regenerate TypeScript client: `cd Cultiv.EnvironmentInspect/Client && npm run generate-client`
4. Rebuild frontend if needed: `npm run build`
5. Run demo site and test in browser: **Settings → Environment Inspector**
6. Test with different configuration scenarios in `appsettings.json`
7. Check browser console for errors
8. Verify backend logs for warnings/errors

### Database Migrations

If you modify the `UserPreference` entity or add new entities:

```bash
cd Cultiv.EnvironmentInspect
dotnet ef migrations add MigrationName --startup-project ../Cultiv.EnvironmentInspect.DemoSite
```

## Branching & Commits

**Branch Strategy:**
- `develop/v2` - Main development branch (default, protected). **All new feature and bugfix development happens here.**
- `release/v2` - Stable release branch (protected)
- `feature/v2/<name>` - Feature branches (branch from develop/v2)
- `hotfix/v2/<name>` - Hotfix branches (branch from release/v2)
- `develop/v3` / `release/v3` - Umbraco 18 line. **Merge-only** — never branch a feature or hotfix directly off it, never commit new business logic there. See "Two Umbraco majors, one repo" below.

**Commit Messages:**
Use [Conventional Commits](https://www.conventionalcommits.org/) for automatic versioning:
- `feat:` - New feature (minor version bump)
- `fix:` - Bug fix (patch version bump)
- `docs:` - Documentation only
- `chore:` - Maintenance tasks
- `feat!:` or `BREAKING CHANGE:` - Breaking change (major version bump)

Example: `feat: add CSV export functionality`

## CI/CD

The project uses GitHub Actions:
- **`ci.yml`**: Builds on Linux (SQLite), runs codegen, validates NuGet package, tests on Windows (SQL Server LocalDB)
- **`release.yml`**: Creates GitHub releases and publishes to NuGet

**Pre-releases**: Run release workflow from `develop/v2`  
**Full releases**: Run release workflow from `release/v2`

Code must pass `dotnet format --verify-no-changes` in CI.

## Common Tasks

### Adding a New Redaction Mode

1. Update `EnvironmentInspectOptions.cs` with new options
2. Implement logic in `EnvironmentInspectService.cs` (e.g., in `ApplyRedaction()` or `RedactNestedKeys()`)
3. Add example to `CONFIGURATION.md` showing input/output
4. Update schema file if needed
5. Run `dotnet format`
6. Test with demo site

### Adding a New Configuration Option

1. Add property to `EnvironmentInspectOptions.cs`
2. Document in `CONFIGURATION.md` reference table
3. Add example usage
4. Update `appsettings-schema.Cultiv.EnvironmentInspect.json`
5. Update demo site's `appsettings.json` with example

### Modifying API Endpoints

Controllers are in `Controllers/` folder. Follow REST conventions:
- Use attribute routing: `[Route("api/environmentinspect/[action]")]`
- Return appropriate status codes
- Document with XML comments for Swagger
- Handle errors and log them

**API Organization Pattern:**
- Base controller: `CultivEnvironmentInspectApiControllerBase` defines base routing and API grouping
- Routing: `[BackOfficeRoute("cultivenvironmentinspect/api/v{version:apiVersion}")]`
- API grouping: `[MapToApi(Constants.ApiName)]` groups endpoints in Swagger for codegen
- Authorization: `[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]` requires Settings section access
- Versioning: `[ApiVersion("1.0")]` and `[MapToApiVersion("1.0")]` on each endpoint

**After modifying API endpoints:**
1. Ensure demo site is running
2. Regenerate TypeScript client: `cd Cultiv.EnvironmentInspect/Client && npm run generate-client`
3. Frontend will now have type-safe access to your new endpoint

## Advanced Patterns

### Reactive Configuration with IOptionsMonitor

Services use `IOptionsMonitor<T>` instead of `IOptions<T>` to react to configuration changes in real-time:

```csharp
public class EnvironmentInspectService : IEnvironmentInspectService
{
    private readonly IOptionsMonitor<EnvironmentInspectOptions> _options;
    
    public EnvironmentInspectService(IOptionsMonitor<EnvironmentInspectOptions> options)
    {
        _options = options;
        
        // React to configuration changes
        _options.OnChange((newOptions, name) => 
        {
            _cache.Clear();
            Task.Run(async () => await PrewarmCacheAsync()); // Re-warm with new config
        });
    }
    
    public void ProcessData()
    {
        var currentOptions = _options.CurrentValue; // Always gets latest config
    }
}
```

### Hybrid Configuration Binding

The `Exclude` and `Redact` arrays support both simple strings and complex objects. This is handled via **post-configuration**:

```csharp
// In Composer
builder.Services.PostConfigure<EnvironmentInspectOptions>(options =>
{
    var excludeSection = config.GetSection("EnvironmentInspect:Exclude");
    if (excludeSection.Exists())
    {
        options.Exclude.Clear();
        foreach (var child in excludeSection.GetChildren())
        {
            if (!string.IsNullOrEmpty(child.Value) && !child.GetChildren().Any())
            {
                // Simple string: "^AZURE_.*"
                options.Exclude.Add(new ExclusionRule { Key = child.Value });
            }
            else
            {
                // Complex object: { "ProviderType": "Azure", "RedactionMode": "Full" }
                var rule = new ExclusionRule();
                child.Bind(rule);
                options.Exclude.Add(rule);
            }
        }
    }
});
```

This allows users to write:
```json
"Exclude": [
  "^APPSETTING_",                                     // String
  { "ProviderType": "AzureKeyVaultConfigurationProvider" }  // Object
]
```

### DbContext Registration

The `EnvironmentInspectDbContext` supports both SQLite and SQL Server with smart connection string resolution:

```csharp
builder.Services.AddDbContext<EnvironmentInspectDbContext>((serviceProvider, options) =>
{
    var connectionString = config.GetConnectionString("EnvironmentInspectDatabase") 
                          ?? "Data Source=|DataDirectory|/EnvironmentInspect.db";
    
    // Replace |DataDirectory| token for SQLite
    if (connectionString.Contains("|DataDirectory|"))
    {
        var umbracoDatabaseFactory = serviceProvider.GetRequiredService<IUmbracoDatabaseFactory>();
        var dataDirectory = umbracoDatabaseFactory.SqlContext.SqlSyntax.GetDataDirectory();
        connectionString = connectionString.Replace("|DataDirectory|", dataDirectory);
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
    
    // Suppress pending model changes warning (cross-database compatibility)
    options.ConfigureWarnings(warnings => 
        warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
});
```

### Graceful Error Handling

When validating user input (e.g., regex patterns), log warnings instead of throwing exceptions:

```csharp
try
{
    var regex = new Regex(pattern);
    // Use regex
}
catch (ArgumentException ex)
{
    _logger.LogWarning(ex, "Invalid regex pattern in configuration: {Pattern}. Skipping rule.", pattern);
    // Continue execution - don't crash the application
}
```

This ensures the dashboard remains usable even if users provide invalid configuration.

## Code Quality Verification (Required After Every Edit)

**After making ANY code changes, you MUST run these checks and verify they pass before reporting completion:**

### Backend Changes (C#)
```bash
dotnet format --verify-no-changes
```
✅ **Expected output**: Should complete with no errors (warnings about workspace loading are OK)  
❌ **If it fails**: Run `dotnet format` to fix issues, then verify again

### Frontend Changes (TypeScript/Client/)
```bash
cd Cultiv.EnvironmentInspect/Client
npm run lint
```
✅ **Expected output**: Command completes with exit code 0, no warnings or errors  
❌ **If it fails**: Fix the linting issues reported, then verify again

### Both Backend and Frontend
If you modified both C# and TypeScript files, run BOTH checks.

**CRITICAL**: Do not claim a fix is complete or CI will pass until you have:
1. Run the appropriate verification command(s)
2. Confirmed the command output shows success (exit code 0, no errors)
3. Reported the verification results to the user

## Anti-Patterns to Avoid

❌ **Don't** claim code is fixed without running and verifying `dotnet format --verify-no-changes` and/or `npm run lint`  
❌ **Don't** commit code that fails `dotnet format`  
❌ **Don't** use real credentials or organization names in examples  
❌ **Don't** add features without updating `CONFIGURATION.md`  
❌ **Don't** use tabs for indentation (use 4 spaces)  
❌ **Don't** ignore logger warnings - they indicate potential issues  
❌ **Don't** modify migrations after they've been committed  

## Two Umbraco majors, one repo

This repo supports Umbraco CMS 17 (`develop/v2`/`release/v2`) and Umbraco CMS 18 (`develop/v3`/`release/v3`) from a single shared history — `develop/v3` was branched from `develop/v2` and almost all source is identical between them. `develop/v2` is where all ongoing feature/bugfix work happens; `develop/v3` is a **merge-only** target.

### Use a worktree, never `git checkout` in place

Work on `develop/v3` from the sibling worktree at `../Cultiv.EnvironmentInspect-v3` (or wherever it was created), not by switching branches inside this checkout. Repeatedly switching branches in one working copy leaves stale artifacts behind — `node_modules` installed against the wrong `@umbraco-cms/backoffice` major, `bin`/`obj` build output from the other major, and a local SQLite runtime database migrated by the wrong Umbraco major (which then refuses to boot). A worktree shares this repo's `.git` history but gets its own working directory, build output, and runtime database, eliminating all of that:

```bash
git worktree add ../Cultiv.EnvironmentInspect-v3 develop/v3   # one-time setup, path/name are just convention
git worktree list                                              # see every worktree and its branch
```

### Porting a change from v2 to v3

**Always use `git merge`, never `git cherry-pick`.** Cherry-picking creates independent commits with no shared ancestry, so the same logical change gets re-flagged as a fresh conflict on every subsequent merge — this is exactly the problem this workflow exists to avoid.

```bash
cd ../Cultiv.EnvironmentInspect-v3     # or wherever the develop/v3 worktree lives
git merge develop/v2                  # never cherry-pick
```

- If the merge touches `Cultiv.EnvironmentInspect/Client/src/**`, rebuild against a running v3 demo site: `npm run generate-client && npm run build`, and fix any call-site breakage from the generated SDK's different style (v3 uses plain function imports, e.g. `getEnvironment()`, instead of v2's class-style `CultivEnvironmentInspectService.getEnvironment()` — rename at each call site, same shape of fix every time).
- Run `dotnet format --verify-no-changes` (and `npm run lint` if Client files changed).
- Push `develop/v3`, confirm CI is green, then fast-forward `release/v3` to match (`git merge develop/v3 --ff-only` from a `release/v3` checkout or worktree) and push that too.
- Skip porting only for changes that are genuinely Umbraco-17-only (e.g. a Swashbuckle-specific fix that has no equivalent on v3's native OpenAPI generator).

### Files that are supposed to always differ

A handful of files are deliberately different between the two branches and marked `merge=ours` in `.gitattributes` — a merge keeps the local branch's version of these without conflicting:

- `.releaserc` — branch names for semantic-release
- `Cultiv.EnvironmentInspect/Cultiv.EnvironmentInspect.csproj` and `Cultiv.EnvironmentInspect.DemoSite/Cultiv.EnvironmentInspect.DemoSite.csproj` — Umbraco package version pins (17.x range vs 18.x)
- `Cultiv.EnvironmentInspect/Composers/CultivEnvironmentInspectComposer.cs` — v2 registers its OpenAPI document via Swashbuckle (`SwaggerGenOptions`/`BackOfficeSecurityRequirementsOperationFilterBase`); v3 uses ASP.NET Core's native OpenAPI generator (`AddBackOfficeOpenApiDocument`/`WithBackOfficeAuthentication`) since Umbraco 18 dropped Swashbuckle
- `Cultiv.EnvironmentInspect/Client/package.json` — `@umbraco-cms/backoffice` major version, plus `@hey-api/openapi-ts`/`vite` versions and the hey-api SDK plugin config (v2 uses `asClass: true`; v3 uses `responseStyle: 'fields'`, which is *why* the generated SDK's call style differs — see above)
- `Cultiv.EnvironmentInspect/Client/scripts/generate-openapi.js` — the OpenAPI discovery URL format changed between majors (`/umbraco/swagger/{name}/swagger.json` on v2 vs `/umbraco/openapi/{name}.json` on v3) plus matching hey-api plugin filtering syntax

`merge.ours.driver` isn't enabled by default — run this once per clone/worktree before merging across these branches:
```bash
git config merge.ours.driver true
```

**Important caveat**: because these files are whole-file `merge=ours`, a *shared* change to one of them on `develop/v2` (e.g. adding a new dependency to `Cultiv.EnvironmentInspect.csproj`, or touching shared logic inside `CultivEnvironmentInspectComposer.cs` like the DbContext registration or cache prewarm) will **not** automatically flow to `develop/v3` — the merge driver silently keeps v3's version. After merging, diff these files against their previous state on v2 (`git log -p develop/v2 -- <file>`) and manually reapply anything that isn't version/OpenAPI-registration-specific to the equivalent spot in v3's copy.

Everything else (business logic, controllers, services, most of the Client) merges normally — that's the whole point of this setup.

## Getting Help

- Check `CONFIGURATION.md` for redaction/exclusion patterns
- Review existing service patterns in `Services/EnvironmentInspectService.cs`
- See `README.md` for user-facing feature documentation
- Check `.github/workflows/` for CI/CD pipeline details
