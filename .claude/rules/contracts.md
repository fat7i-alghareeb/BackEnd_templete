---
paths:
  - "src/Taxi.Contracts/**"
  - "src/Taxi.Api/Resources/**"
---

# Contracts and localization rules (loaded for `src/Taxi.Contracts/` and the resource files)

Copy `src/Taxi.Contracts/Requests/Cars/CreateCarRequest.cs`.

- Requests are plain `class`es with `get; set;`, `DataAnnotations` and **no methods**. Non-nullable
  strings default to `string.Empty`. References nothing, not even a package.
- Every attribute's `ErrorMessage` is a `LocalizationKeys` constant (`[Required]`, `[StringLength]`,
  `[Range]`, `[EmailAddress]`), never English.
- Bilingual fields arrive flat: `TitleEn` / `TitleAr`. An update request has no `Id` (the route
  has it).
- Query-string requests use `[Range]` / `[StringLength]` and match `Paging` (page 1, size 20, max 100,
  clamped). Sort fields are a whitelist (`CarSortFields`), never a column name.
- Never write `"en"` / `"ar"`: use `Languages.En`, `Languages.Ar`, `Languages.Default`.

**Adding a key** touches three places, always: `LocalizationKeys.cs` (nested class, PascalCase name,
dotted value `Area.Field.Condition`), `SharedResource.en.json` and `SharedResource.ar.json`. Nothing
in the build catches a key missing from one file, so check both. `/add-key` does all three. Arguments
use `{0}` placeholders. `TODO-AR:` prefixed values (from the generator) must be translated.

Details only if needed: `src/Taxi.Contracts/Contracts_Layer_Blueprint.md` §5–§7.
