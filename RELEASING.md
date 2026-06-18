# Publikacja i osobne repozytorium

Ten katalog jest przygotowany jako **samodzielne repozytorium open source**. Gotowy instalator **nie trafia do gita** — tylko do GitHub Releases po zbudowaniu w CI.

## 1. Utwórz repo na nowym koncie GitHub

1. Zaloguj się na **nowe konto** GitHub (dedykowane dla OSS konektora).
2. Utwórz publiczne repo, np. `ksef-hub-connector`.
3. Włącz **2FA** na koncie (wymagane m.in. przez SignPath Foundation).

## 2. Wypchnij kod (pierwszy raz)

W PowerShell, z katalogu `Connector` (ten folder = root nowego repo):

```powershell
git init
git add .
git commit -m "Initial release: KSeF Hub Connector 1.0.0"
git branch -M main
git remote add origin https://github.com/TWOJE-KONTO/ksef-hub-connector.git
git push -u origin main
```

## 3. Pierwszy release 1.0.0

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Workflow `.github/workflows/release.yml` zbuduje `KSeFHubConnectorSetup.exe` i dołączy go do GitHub Release.

Adres pobrania będzie w formie:

`https://github.com/TWOJE-KONTO/ksef-hub-connector/releases/download/v1.0.0/KSeFHubConnectorSetup.exe`

## 4. Podpis certyfikatem open source (SignPath)

Szczegóły: [SIGNING.md](./SIGNING.md)

Skrót:

1. Złóż wniosek do [SignPath Foundation](https://signpath.org/).
2. Podaj URL publicznego repo i licencję MIT.
3. Skonfiguruj **GitHub Actions** jako trusted build system.
4. Po zatwierdzeniu — podpisuj każdy release z CI (nie lokalny EXE z laptopa).

## 5. Integracja z KSeF Hub (SaaS)

W głównym projekcie ustaw URL instalatora na release z nowego repo, np. zmienną środowiskową:

`NEXT_PUBLIC_CONNECTOR_INSTALLER_URL=https://github.com/TWOJE-KONTO/ksef-hub-connector/releases/download/v1.0.0/KSeFHubConnectorSetup.exe`

Po podpisaniu binarium przez SignPath użytkownicy Windows zobaczą wydawcę **SignPath Foundation**.

## 6. Co aktualizować przy kolejnych wersjach

1. Podnieś wersję w `Directory.Build.props`.
2. Dodaj wpis w `CHANGELOG.md`.
3. Commit, tag `v1.x.x`, push taga.
4. Zweryfikuj artefakt w GitHub Releases (i podpis, jeśli skonfigurowany).
