# Publikacja release

Gotowy instalator **nie trafia do gita** — tylko do [GitHub Releases](https://github.com/nuteat-systems/ksef-hub-connector/releases). Podpis odbywa się lokalnie certyfikatem Certum SimplySign (szczegóły: [SIGNING.md](./SIGNING.md)).

## 1. Przygotowanie wersji

1. Podnieś wersję w `Directory.Build.props`.
2. Dodaj wpis w `CHANGELOG.md`.
3. Commit na `main` i push:

```powershell
git add Directory.Build.props CHANGELOG.md
git commit -m "Release 1.x.x"
git push origin main
```

## 2. Tag i build w CI

```powershell
git tag v1.x.x
git push origin v1.x.x
```

Workflow `.github/workflows/release.yml` zbuduje `KSeFHubConnectorSetup.exe` i utworzy GitHub Release z **niepodpisanym** plikiem.

Adres pobrania (przykład dla `v1.0.0`):

`https://github.com/nuteat-systems/ksef-hub-connector/releases/download/v1.0.0/KSeFHubConnectorSetup.exe`

## 3. Podpis i podmiana assetu

1. Zaloguj się w **SimplySign Desktop** (ikona w zasobniku).
2. Zbuduj z tego samego taga i podpisz — pełna procedura w [SIGNING.md](./SIGNING.md).
3. W GitHub Releases usuń niepodpisany EXE i wgraj podpisany (lub `gh release upload ... --clobber`).

**Ważne:** nie pushuj ponownie tego samego taga — CI nadpisze podpisany plik.

## 4. Integracja z KSeF Hub (SaaS)

W głównym projekcie ustaw URL instalatora, np.:

`NEXT_PUBLIC_CONNECTOR_INSTALLER_URL=https://github.com/nuteat-systems/ksef-hub-connector/releases/download/v1.0.0/KSeFHubConnectorSetup.exe`

Przy nowej wersji zmień segment `v1.0.0` na aktualny tag.

## 5. Checklist po release

- [ ] Asset w Releases jest podpisany (`signtool verify` na pobranym pliku)
- [ ] Wydawca: Open Source Developer Grzegorz Jezierski
- [ ] URL w KSeF Hub wskazuje na właściwy tag
- [ ] Opis release na GitHubie wspomina o podpisie (opcjonalnie)

## Maintainer

**Grzegorz Jezierski** — grzesiek0012@gmail.com

Repozytorium: https://github.com/nuteat-systems/ksef-hub-connector
