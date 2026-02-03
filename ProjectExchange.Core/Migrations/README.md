# Migrations (reset – create new initial migration)

All previous migrations were removed so we can start fresh with **snake_case** tables from the beginning.

## Next step

Create a new initial migration (generates snake_case tables via `UseSnakeCaseNamingConvention`):

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=projectexchange;Username=postgres;Password=postgres" dotnet ef migrations add Initial --project ProjectExchange.Core
```

Then apply it (drop and recreate the database if you had PascalCase tables, or use a new database name):

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=projectexchange;Username=postgres;Password=postgres" dotnet ef database update --project ProjectExchange.Core
```

After that, run tests:

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=projectexchange;Username=postgres;Password=postgres" dotnet test
```
