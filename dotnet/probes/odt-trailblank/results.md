# ODT trailing-blank decorations (tdf#168777)

Reference: LibreOfficeDev 26.8.3.0.0 9dcd8a837588. `gen-trail.py` writes an underlined and a struck-through
paragraph; the daily converts it to `.odt` (a flat ODF's `office:settings` are ignored, so the variants are
the zipped file with `settings.xml` patched). `rules.py` lists every thin horizontal rule.

| `MsWordCompTrailingBlanks` / `MsWordUlTrailSpace` | reference: 3 pt rule over each wrapped line's trailing blank | before | after |
|---|---|---|---|
| false / false (what LibreOffice writes for a native document) | yes, on 8 lines | none | yes, 8 |
| true / false | none | none | none |
| true / true | yes | none | yes |
| false / true | yes | none | yes |

No `.odt` in `test_documents` states either item, so every one of them decorates.
