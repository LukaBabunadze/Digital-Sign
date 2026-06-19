# Digital Sign Documents

Blazor Server web application for uploading documents and collecting multiple digital signatures in a predefined order. Users generate signing keys in the browser, keep the private key locally, upload only the public key, and each accepted signature is recorded in a simplified blockchain table.

## Platform Instructions

- [macOS setup](MACOS.md)
- [Windows setup](WINDOWS.md)

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
