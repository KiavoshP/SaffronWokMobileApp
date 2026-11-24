# SaffronWok Mobile

The first development slice shows sample live and upcoming events with venue-specific menus. With foreground location permission, the app suggests events within five miles, with live events first and nearest first within each group. Customers can always choose an event manually. Mobile ordering is marked coming soon.

## Run the development API

Use the .NET 10 SDK. From the repository root:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/SaffronWok.Api --no-launch-profile --urls http://localhost:5000
```

The sample catalog is enabled only in Development. Use `GET /api/events` for the event list and `GET /api/events/smorgasburg/menu` or `GET /api/events/pontoon/menu` for a sample menu.

## Run the mobile app

Install the .NET MAUI Android workload and Android SDK for Android development. For iOS and iPad, use a Mac with the iOS MAUI workload and compatible Xcode. In your IDE, run `src/SaffronWok.Mobile` on a simulator or emulator. The local API address is `http://10.0.2.2:5000` for Android Emulator and `http://localhost:5000` for iOS Simulator. The Android cleartext exception is scoped to the emulator's host alias. Physical-device testing needs a reachable HTTPS endpoint and an updated API address in `MauiProgram.cs`.

## POS integration

The SaffPOS integration branch is `deploy/ec2-package`, commit `129ef73`. Its order and catalog endpoints require staff authentication and its current catalog is global. The development catalog in this repository is illustrative. Publishing real event menus requires location-scoped data managed in SaffPOS. Customer order history and points require a verified customer identity and a customer-scoped SaffPOS read API.

## Test phone verification locally

Run PostgreSQL and the API from the same shell with Docker Compose and OpenSSL installed. Keep the database password and signing key in your local environment or secret store. Reuse the same database password across restarts of the development database.

```sh
export SAFFRON_POSTGRES_PASSWORD="$(openssl rand -hex 24)"
export PhoneVerification__SigningKey="$(openssl rand -base64 32)"
export ConnectionStrings__Verification="Host=127.0.0.1;Database=saffronwok;Username=saffronwok;Password=$SAFFRON_POSTGRES_PASSWORD"
export PhoneVerification__EnableLocalTestCode=true
docker compose up -d --wait
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/SaffronWok.Api --no-launch-profile --urls http://localhost:5000
```

From the app, request a code for the fictional number `+14045550123` and enter `021819`. The local test code requires the explicit opt-in above, Development mode, a loopback connection, and a North American fictional number ending in 555-0100 through 555-0199. It still requires a current challenge, and the resulting test receipt is marked in the database.

The API stores a keyed hash of each generated code. Challenges expire after five minutes, allow five attempts, and are consumed on success. A successful confirmation issues a ten-minute verification receipt. Its hash is stored in PostgreSQL. Receipt exchange for a customer session will be part of the account integration.

Development applies [the verification schema](src/SaffronWok.Api/Verification/schema.sql) at startup when configured. Apply that schema separately to production before enabling the verification endpoints. Production delivery requires an implementation of `IPhoneCodeSender`; the request endpoint returns HTTP 503 when delivery is unavailable. The mobile app currently uses local emulator API addresses. Use HTTPS for production verification traffic.

## GitHub Actions

The Build workflow compiles the API in Release and the Android and iOS Simulator apps in Debug. It runs on pushes to `main`, pull requests targeting `main`, and manual dispatch. The iOS job uses Xcode 26.5 and an ARM64 simulator target, with Apple code signing disabled.

TestFlight or App Store distribution requires an Apple Developer Program team, a registered app ID for `com.saffronwok.mobile`, an Apple Distribution certificate with its private key, and an App Store provisioning profile. To automate upload, CI also needs an authorized App Store Connect API key. Add those credentials to a protected GitHub environment when setting up distribution.
