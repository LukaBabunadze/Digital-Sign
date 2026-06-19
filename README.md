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

## Detailed Workflow

1. User registration
   1. Open the app and create a normal user account with the required profile fields.
   2. Wait for the account to be approved by an administrator before attempting to enroll a signing key.
   3. Keep the email address consistent, because it is used to match the user with signing and notification records.

2. Admin approval and registration key issuance
   1. Sign in with the seeded admin account.
   2. Open `Admin` and review the pending registration list.
   3. Approve the user to activate the account.
   4. Copy the issued registration key and send it to the user out of band.

3. Browser key generation
   1. Sign in as the approved user and open `Signing Keys`.
   2. Generate a browser key pair in the app.
   3. Save the downloaded private key file locally as the only copy of the private key.
   4. Keep the public key ready for upload; it is the only key material the app stores server-side.

4. Public key enrollment
   1. Upload the public key and the registration key from the `Signing Keys` page.
   2. The app validates that the public key is a PEM-encoded public key and that the registration key is still valid.
   3. After enrollment succeeds, the user can participate in signing workflows.

5. Document creation and signer ordering
   1. Open `Documents` and upload the document that needs signatures.
   2. Add the signer emails in the exact order they must sign.
   3. The first signer is marked as awaiting signature, and the remaining signers stay pending until earlier signatures complete.
   4. Submit the signing request to create the signing process.

6. Signing cycle
   1. The first signer opens the notification for the document.
   2. The signer loads the locally saved private key file in the browser.
   3. The app builds the signing payload for the current signing position and verifies the resulting signature against the stored public key.
   4. When the signature is accepted, the next signer is advanced into the awaiting state.
   5. Repeat the same sign-and-advance cycle until every signer has completed their turn.

7. Completion and follow-up
   1. When the last signature is recorded, the document becomes active.
   2. Review the document details to confirm the signing history and final status.
   3. If a signer rejects or withdraws, the process is suspended and must be canceled or restarted depending on the business decision.

## Tests

Run all tests:

```bash
dotnet test DigitalSignDocuments.slnx
```

The test suite covers ordered signing, rejection/suspension, withdrawal before completion, registration-key validation, and blockchain chaining.
