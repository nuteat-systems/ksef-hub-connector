# KSeF Hub Connector

Lokalny konektor Windows dla [KSeF Hub](https://ksefhub.app) — łączy SaaS z bazami WAPRO MAG/FAKIR u klienta bez wystawiania SQL Servera do Internetu.

## Funkcje

- **Windows Service** — stałe połączenie gRPC z hubem (heartbeat, reconnect).
- **SQL executor** — SELECT, NON_QUERY, stored procedures; sesje SQL dla transakcji WAPRO.
- **Konfigurator WPF** — ustawienia, testy połączenia, restart usługi.
- **Instalator EXE** — instalacja usługi i konfiguratora jednym kliknięciem.

## Wymagania

- Windows 10/11 lub Windows Server
- Dostęp sieciowy do SQL Servera WAPRO (lokalnie lub w LAN)
- Konto konektora utworzone w KSeF Hub (`Konfiguracja > Konektor`)

## Instalacja (użytkownik końcowy)

Pobierz `KSeFHubConnectorSetup.exe` z [GitHub Releases](https://github.com/nuteat-systems/ksef-hub-connector/releases) i uruchom jako administrator.

Konfiguracja zapisywana jest w:

`%ProgramData%\KSeFHub\Connector\connector.settings.json`

Hasło SQL i token dostępu są szyfrowane DPAPI (`LocalMachine`).

Official Windows installers are code-signed through the [SignPath Foundation](https://signpath.org/) open-source program (publisher: SignPath Foundation). Details: [SIGNING.md](./SIGNING.md).

## Konfiguracja

1. W KSeF Hub utwórz konektor i skopiuj **Connector ID** oraz **token**.
2. W konfiguratorze ustaw adres serwera (domyślnie `https://connector.ksefhub.app`).
3. Skonfiguruj SQL — lokalnie możesz użyć Windows Auth; dla zdalnego hosta (IP) użyj loginu i hasła SQL.
4. **Zapisz** → **Test gRPC** → **Test SQL** → **Restart usługi**.

## Build ze źródeł

Wymagany [.NET SDK 8+](https://dotnet.microsoft.com/download).

```powershell
dotnet restore .\Connector.sln
dotnet build .\Connector.sln -c Release
dotnet test .\Connector.Tests\Connector.Tests.csproj -c Release
```

Instalator:

```powershell
.\build-installer.ps1
# wynik: .\artifacts\KSeFHubConnectorSetup.exe
```

## Struktura projektu

| Katalog | Opis |
|---------|------|
| `Connector.Service` | Usługa Windows + gRPC + SQL |
| `Connector.Configurator` | UI WPF |
| `Connector.Contracts` | Kontrakt `connector.proto` |
| `Connector.Shared` | Ustawienia, DPAPI, connection string |
| `Connector.Installer` | Instalator self-contained |
| `Connector.Tests` | Testy jednostkowe |

## Publikacja i podpis

- **Release:** tag `v1.0.0` → GitHub Actions buduje instalator i publikuje w Releases.
- **Open source signing:** [SIGNING.md](./SIGNING.md) (SignPath Foundation).
- **Maintainerzy:** [RELEASING.md](./RELEASING.md).

Gotowy instalator (`KSeFHubConnectorSetup.exe`) **nie jest commitowany** do repozytorium — jest budowany w GitHub Actions i publikowany wyłącznie w [GitHub Releases](https://github.com/nuteat-systems/ksef-hub-connector/releases).

## Maintainer

This open-source project is maintained by:

**Grzegorz Jezierski**

Contact: grzesiek0012@gmail.com

## Licencja

MIT — zobacz [LICENSE](./LICENSE).

## Powiązane projekty

Backend KSeF Hub (SaaS) jest osobnym repozytorium. Konektor komunikuje się z hubem wyłącznie przez gRPC + token Bearer.
