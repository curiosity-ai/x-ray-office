# ooo:configuration-settings needs its prefix declared, for every setting

Four fodt: TabsRelativeToIndent true/false, with and without xmlns:ooo. The 26.8 daily puts B at x 272.80 / 200.80 / 272.80 / 272.80; before the fix we gave 200.69 for the undeclared false, after 272.69 (the 0.1 pt writer offset). Reach: 0 of the 65 ODT/FODT in test_documents and tests/corpus that carry a configuration set leave the prefix undeclared.
