# Podpisywanie instalatora (open source)

Dla publicznego repozytorium zalecany jest program **[SignPath Foundation](https://signpath.org/)** — darmowy podpis kodu dla projektów OSS (wydawca w Windows: *SignPath Foundation*).

## Wymagania wstępne

| Wymaganie | Ten projekt |
|-----------|-------------|
| Licencja OSI | MIT (`LICENSE`) |
| Repo publiczne | Tak |
| Brak zamkniętego kodu w release | Tylko build z tego repo |
| MFA na GitHub | Włącz na koncie maintainera |
| Opis funkcji | `README.md` |
| Build z CI | `.github/workflows/release.yml` |
| Spójna wersja w pliku | `Directory.Build.props` → `1.0.0` |

## Czego nie robić

- Nie commituj `KSeFHubConnectorSetup.exe` do gita.
- Nie podpisuj ręcznie buildu z dev machine jako „oficjalnego” release.
- Nie mieszaj w release binariów z innego źródła niż tag + GitHub Actions.

## Typowy flow po akceptacji w SignPath

1. SignPath łączy się z Twoim repo GitHub (trusted build).
2. Workflow `release.yml` buduje EXE i przekazuje artefakt do SignPath (integracja według dokumentacji SignPath).
3. Maintainer zatwierdza signing request w panelu SignPath.
4. Podpisany EXE trafia do GitHub Release (lub pobierasz i uploadujesz ręcznie do release — zależnie od integracji).

Dokumentacja SignPath:

- [Terms for OSS projects](https://signpath.org/terms.html)
- [Trusted build systems](https://docs.signpath.io/trusted-build-systems/)
- [Build system integration](https://docs.signpath.io/build-system-integration)

## Alternatywa

Klasyczny certyfikat komercyjny (EV/OV) — płatny, wydawca to Twoja organizacja. Dla czystego OSS SignPath jest zwykle lepszym wyborem ekonomicznym.
