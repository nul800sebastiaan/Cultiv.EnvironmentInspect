# Cultiv.EnvironmentInspect Development Guidelines

## Project Overview

Cultiv Environment Inspector is an Umbraco CMS package (v17+) that provides a dashboard in the Settings section showing all configuration values and their sources (appsettings.json, environment variables, Azure Key Vault, etc.).

**Key Technologies:**
- **Backend**: ASP.NET Core (net10.0), Umbraco CMS v17+, EF Core
- **Frontend**: TypeScript, Vue.js, Vite (in `/Client` folder)
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
- Vue.js for UI components
- Vite for building
- ESLint for linting

Build the frontend with:
```bash
cd Cultiv.EnvironmentInspect/Client
npm run build
```

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

1. Start the demo site: `dotnet run` in `Cultiv.EnvironmentInspect.DemoSite/`
2. Access Umbraco at `https://localhost:5001` (or configured port)
3. Navigate to **Settings → Environment Inspector**

### Database Migrations

If you modify the `UserPreference` entity or add new entities:

```bash
cd Cultiv.EnvironmentInspect
dotnet ef migrations add MigrationName --startup-project ../Cultiv.EnvironmentInspect.DemoSite
```

## CI/CD

The project uses GitHub Actions:
- **`ci.yml`**: Builds, tests, formats check
- **`release.yml`**: Creates GitHub releases and publishes to NuGet

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

## Anti-Patterns to Avoid

❌ **Don't** commit code that fails `dotnet format`  
❌ **Don't** use real credentials or organization names in examples  
❌ **Don't** add features without updating `CONFIGURATION.md`  
❌ **Don't** use tabs for indentation (use 4 spaces)  
❌ **Don't** ignore logger warnings - they indicate potential issues  
❌ **Don't** modify migrations after they've been committed  

## Getting Help

- Check `CONFIGURATION.md` for redaction/exclusion patterns
- Review existing service patterns in `Services/EnvironmentInspectService.cs`
- See `README.md` for user-facing feature documentation
- Check `.github/workflows/` for CI/CD pipeline details
