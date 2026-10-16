# Localization

Arabic (ar-SA) is the neutral language in `Strings.resx`; English is in `Strings.en.resx`. Both files always have identical keys, sorted alphabetically. `LocalizationTests` enforces this.

## Key naming

`Feature_Element_Purpose` in PascalCase segments: `People_Search_Placeholder`, `Common_Save`, `Error_Database_Locked`.
Plurals: `Base_zero`, `Base_one`, `Base_two`, `Base_few`, `Base_many`, `Base_other`, used through `Tr.Plural`.

## Glossary

| English | Arabic | Notes |
|---|---|---|
| Person | شخص | |
| Save | حفظ | |
| Cancel | إلغاء | |
| Retry | إعادة المحاولة | |
