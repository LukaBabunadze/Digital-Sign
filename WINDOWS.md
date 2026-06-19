# Windows Instructions

## Prerequisites

- .NET SDK 10.0 or newer
- SQL Server, SQL Server Express, or Docker Desktop
- A modern browser with Web Crypto API support

## Database Options

Use either a local SQL Server instance or Docker.

### Option 1: Docker

```powershell
docker run -e "ACCEPT_EULA=Y" `
  -e "MSSQL_SA_PASSWORD=Your_strong_password123" `
  -p 1433:1433 `
  --name digital-sign-sql `
  -d mcr.microsoft.com/mssql/server:2022-latest
```

### Option 2: Local SQL Server

If you already have SQL Server installed, point the app to that instance in `src/DigitalSignDocuments.Web/appsettings.json`.

## Configure the Database

Set the connection string in `src/DigitalSignDocuments.Web/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=DigitalSignDocuments;User Id=sa;Password=Your_strong_password123;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

## Restore and Migrate

From the repository root:

```powershell
dotnet tool restore
dotnet restore DigitalSignDocuments.slnx
dotnet tool run dotnet-ef database update `
  --project src/DigitalSignDocuments.Web/DigitalSignDocuments.Web.csproj `
  --startup-project src/DigitalSignDocuments.Web/DigitalSignDocuments.Web.csproj
```

## Run the App

```powershell
dotnet run --project src/DigitalSignDocuments.Web/DigitalSignDocuments.Web.csproj
```

Open one of the local URLs shown in the console, usually:

- `https://localhost:7288`
- `http://localhost:5251`

If HTTPS is not trusted locally:

```powershell
dotnet dev-certs https --trust
```

## Notes

- The browser generates the signing key pair.
- The private key is downloaded locally and never uploaded.
- The admin account is seeded from `appsettings.json`:
  - Email: `admin@example.com`
  - Password: `Admin123!`
