# Digital Sign Documents

Blazor Server web application for uploading documents and collecting multiple digital signatures in a predefined order. Users generate signing keys in the browser, keep the private key locally, upload only the public key, and each accepted signature is recorded in a simplified blockchain table.

## Requirements

- .NET SDK 10.0 or newer
- SQL Server
- A modern browser with Web Crypto API support

On macOS, the default generated connection string uses SQL Server LocalDB, which is Windows-only. Use a SQL Server instance or Docker container instead.

Example SQL Server container:

```bash
docker run -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=Your_strong_password123" \
  -p 1433:1433 \
  --name digital-sign-sql \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

## Configure The Database

Set the connection string in `src/DigitalSignDocuments.Web/appsettings.json`.

For the Docker example above:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=DigitalSignDocuments;User Id=sa;Password=Your_strong_password123;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

## Restore Tools And Packages

From the repository root:

```bash
dotnet tool restore
dotnet restore DigitalSignDocuments.slnx
```

## Create The Database

Run the EF Core migrations:

```bash
dotnet tool run dotnet-ef database update \
  --project src/DigitalSignDocuments.Web/DigitalSignDocuments.Web.csproj \
  --startup-project src/DigitalSignDocuments.Web/DigitalSignDocuments.Web.csproj
```

## Run The Application

```bash
dotnet run --project src/DigitalSignDocuments.Web/DigitalSignDocuments.Web.csproj
```

Open one of the configured local URLs:

- `https://localhost:7288`
- `http://localhost:5251`

If HTTPS is not trusted locally, run:

```bash
dotnet dev-certs https --trust
```

## Default Admin Account

The app seeds an admin user from `appsettings.json`:

- Email: `admin@example.com`
- Password: `Admin123!`

Use this account to approve registrations and issue registration keys.

## Basic Workflow

1. Register a normal user with first name, last name, date of birth, email, and password.
2. Log in as the admin and open `Admin`.
3. Approve the user and copy the issued registration key.
4. Log in as the user and open `Signing Keys`.
5. Generate a browser key pair. The private key downloads locally as a `.pem` file.
6. Upload the public key with the registration key.
7. Upload a document from `Documents` and enter signatory emails in signing order.
8. Each signer opens their notification, loads their local private key file, and signs.
9. After all signatures are collected, the document becomes active.

## Tests

Run all tests:

```bash
dotnet test DigitalSignDocuments.slnx
```

The test suite covers ordered signing, rejection/suspension, withdrawal before completion, registration-key validation, and blockchain chaining.
