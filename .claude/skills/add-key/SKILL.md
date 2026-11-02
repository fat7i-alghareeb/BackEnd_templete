---
name: add-key
description: Add a localization key to LocalizationKeys.cs and both language files.
argument-hint: "[Area.Field.Condition] [English text] [Arabic text]"
disable-model-invocation: true
---

# Add key: $ARGUMENTS

1. `pwsh ./tool/catalog.ps1 <main word>` and `grep` both `src/Taxi.Api/Resources/SharedResource.*.json`
   for the English and the Arabic sentence. If one exists, reuse its key and stop.
2. Add the constant to the right nested class in `src/Taxi.Contracts/Common/LocalizationKeys.cs`
   (entity keys in the entity's class; request-shape messages in `Validation`). Name: PascalCase
   condition; value: dotted `Area.Field.Condition`.
3. Add the **same key** to **both** `SharedResource.en.json` and `SharedResource.ar.json` (keep the
   existing file style; a message with an argument uses `{0}`). If the Arabic is missing from the
   arguments, propose a natural one and ask me to confirm; never leave English in the Arabic file.
4. `dotnet build src/Taxi.Contracts` (fast check). Reply with the constant name
   (`LocalizationKeys.Area.Name`) only.
