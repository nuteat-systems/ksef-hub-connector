# Podpisywanie instalatora (open source)

Oficjalne instalatory są podpisywane certyfikatem **[Certum Open Source Code Signing](https://www.certum.pl/pl/certyfikat-code-signing/)** w chmurze (SimplySign). W Windows wydawca to **Open Source Developer Grzegorz Jezierski**.

Klucz prywatny pozostaje w chmurze Certum — do podpisu potrzebne są aplikacje **SimplySign** (telefon + desktop), nie wystarczy sam plik PEM/DER.

## Wymagania wstępne

| Wymaganie | Ten projekt |
|-----------|-------------|
| Licencja OSI | MIT (`LICENSE`) |
| Repo publiczne | Tak |
| Certyfikat OSS | Certum Open Source Code Signing (SimplySign) |
| SimplySign Desktop | Zalogowany (ikona w zasobniku) |
| SimplySign Mobile | Android lub iOS (kody OTP) |
| Windows SDK | `signtool.exe` (Windows Kits) |
| Build z CI | `.github/workflows/release.yml` |
| Spójna wersja | `Directory.Build.props` |

Dokumentacja Certum:

- [Aktywacja i podpisywanie SimplySign (PDF)](https://files.certum.eu/documents/manual_pl/instrukcja-aktywacji-i-podpisywania-PL-w-chmurze-1.0.pdf)
- [signtool i jarsigner w chmurze (PDF)](https://files.certum.eu/documents/manual_pl/CS-Code_Signing_w_chmurze_Podpisywanie_signtool_jarsigner.pdf)

## Czego nie robić

- Nie commituj `KSeFHubConnectorSetup.exe` do gita.
- Nie publikuj binarium zbudowanego z losowego commita — build musi odpowiadać tagowi release.
- Nie pushuj ponownie istniejącego taga — CI nadpisze podpisany plik w Releases **niepodpisanym** EXE.

## Flow release (CI + podpis lokalny)

GitHub Actions nie ma dostępu do SimplySign, więc podpis jest dwuetapowy:

1. Push taga `v*.*.*` → workflow `release.yml` buduje instalator i publikuje **niepodpisany** EXE w GitHub Releases.
2. Maintainer buduje ten sam tag lokalnie, podpisuje `signtool` i **podmienia** asset w release.

### 1. Build z taga

```powershell
git fetch origin --tags
git checkout v1.0.0   # lub aktualny tag

.\build-installer.ps1
```

### 2. Sprawdź certyfikat

SimplySign Desktop musi być połączony. PowerShell **nie** uruchamiaj jako Administrator.

```powershell
Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
  Format-List Subject, Thumbprint, NotAfter
```

### 3. Podpisz instalator

`signtool` zwykle nie jest w PATH — użyj pełnej ścieżki z Windows Kits (wersja katalogu może się różnić):

```powershell
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
$exe = ".\artifacts\KSeFHubConnectorSetup.exe"
$thumb = "WKLEJ_ODCISK_PALCA"   # Thumbprint z kroku 2

& $signtool sign `
  /sha1 $thumb `
  /tr http://time.certum.pl `
  /td sha256 `
  /fd sha256 `
  /v $exe

& $signtool verify /pa /all $exe
```

### 4. Wgraj podpisany plik do GitHub Release

**Przeglądarka:** Releases → Edit → usuń stary `KSeFHubConnectorSetup.exe` → przeciągnij podpisany plik.

**GitHub CLI:**

```powershell
gh release upload v1.0.0 .\artifacts\KSeFHubConnectorSetup.exe `
  --repo nuteat-systems/ksef-hub-connector `
  --clobber
```

### 5. Weryfikacja po publikacji

Pobierz EXE z Releases (nie lokalną kopię) i sprawdź podpis:

```powershell
Invoke-WebRequest `
  -Uri "https://github.com/nuteat-systems/ksef-hub-connector/releases/download/v1.0.0/KSeFHubConnectorSetup.exe" `
  -OutFile "$env:TEMP\KSeFHubConnectorSetup.exe"

& $signtool verify /pa /all "$env:TEMP\KSeFHubConnectorSetup.exe"
```

## Co widzi użytkownik

- Podpis cyfrowy: **Open Source Developer Grzegorz Jezierski** (Certum Code Signing 2021 CA).
- Brak komunikatu „nieznany wydawca”.
- Microsoft SmartScreen może nadal ostrzegać przy pierwszych pobraniach (to normalne dla certyfikatów nie-EV; reputacja buduje się z czasem).

## Alternatywa: SignPath Foundation

Darmowy podpis OSS z wydawcą *SignPath Foundation* i integracją CI — [signpath.org](https://signpath.org/). Ten projekt korzysta z Certum SimplySign z powodu prostszego setupu lokalnego.
