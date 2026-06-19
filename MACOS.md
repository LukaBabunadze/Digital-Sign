# macOS Instructions

## Prerequisites

- .NET SDK 10.0 or newer
- Docker Desktop
- A modern browser with Web Crypto API support

## Start SQL Server

```bash
docker run -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=Your_strong_password123" \
  -p 1433:1433 \
  --name digital-sign-sql \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

## Configure the Database

Set the connection string in `src/DigitalSignDocuments.Web/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=DigitalSignDocuments;User Id=sa;Password=Your_strong_password123;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

## Restore and Migrate

From the repository root:

```bash
dotnet tool restore
dotnet restore DigitalSignDocuments.slnx
dotnet tool run dotnet-ef database update \
  --project src/DigitalSignDocuments.Web/DigitalSignDocuments.Web.csproj \
  --startup-project src/DigitalSignDocuments.Web/DigitalSignDocuments.Web.csproj
```

## Run the App

```bash
dotnet run --project src/DigitalSignDocuments.Web/DigitalSignDocuments.Web.csproj --urls http://localhost:5251
```

Open:

- `http://localhost:5251`

## Notes

- The browser generates the signing key pair.
- The private key is downloaded locally and never uploaded.
- The admin account is seeded from `appsettings.json`:
  - Email: `admin@example.com`
  - Password: `Admin123!`
