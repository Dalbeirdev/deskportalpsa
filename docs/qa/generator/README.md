# Master test plan generator

Builds two deliverables from one set of cases — 238 across 23 modules covering authentication,
tenant isolation, roles and permissions, technician registration, PSA connections, field
mapping, the sync engine, ticket lifecycle, time tracking, analytics, the client control panel
and operations:

| Output | For | Built by |
| --- | --- | --- |
| `../desk-portal-master-test-plan.pdf` | reading, review, sign-off | `build_qa_pdf.py` |
| `../desk-portal-test-cases.xlsx` | testers recording results | `build_qa_xlsx.py` |

## Regenerate

```bash
pip install reportlab openpyxl
python build_qa_pdf.py
python build_qa_xlsx.py
```

Both write one directory up, over the committed copies.

The PDF build is deterministic (`invariant=1`), so rebuilding without changing a case produces
a byte-identical file and no git diff — a diff on that PDF means the plan actually changed.
The workbook is **not** byte-reproducible (openpyxl stamps a zip timestamp per entry), so
expect it to show as modified on every rebuild; only regenerate it when a case really changed.

### The workbook

Four sheets. Testers fill in only the four shaded columns on **Test Cases** — Result (a
dropdown that colours itself), Tester, Date, Notes. **Summary** is entirely `COUNTIFS` over
that sheet, so it keeps itself current; nothing on it should be typed into. **How to use**
carries the legend and a worked example row, **Reference** the SQL cookbook and log lines.

If you add or rename a module, the Summary labels must match the `Module` column text
*exactly* — they are the `COUNTIF` criteria. A mismatch does not error, it silently counts
zero, so re-run the check in the commit message for this file if you change module names.

Formulas are limited to `COUNTIF`, `COUNTIFS`, `SUM` and `IFERROR` on purpose: all pre-2007,
so they parse in Excel and LibreOffice alike. Avoid `XLOOKUP`, `FILTER`, `UNIQUE` and friends —
openpyxl writes no spill metadata, so they produce a file that looks fine and is wrong.

## Editing the plan

The cases are plain data. `build_qa_pdf.py` is layout only; don't put content in it.

- `qa_plan_part1.py` — the `mod()` helper plus modules 1–6 (environment, auth, security,
  roles, users, org structure)
- `qa_plan_part2.py` — modules 7–10 (client portal, connections, mapping, sync engine)
- `qa_plan_part3.py` — modules 11–16 (tickets, notes, time, attachments, analytics, rollup)
- `qa_plan_part4.py` — modules 17–23 (control panel, reports, assistant, ops, public site,
  performance, regression traps)

Each case is a five-tuple:

```python
("SYNC-02", "Pagination reads past the first page",
 "<how to test — concrete steps, real endpoints, real SQL>",
 "<what should happen>",
 "<where to look to prove it: UI, API, SQL or log>")
```

Add a module with `mod(name, intro, cases)`. Module order in the PDF follows import order in
`build_qa_pdf.py`, so a new part file must be imported there too.

## Conventions worth keeping

The value of this plan is in the fifth field. This system fails silently — an unmapped value, a
skipped ticket and a truncated import all look exactly like normal successful operation — so
wherever a passing screen would not settle the question, name the query or log line that does.

Text is XML-escaped on the way into ReportLab, so `&`, `<` and `>` are safe in case text. Avoid
Unicode sub/superscripts: the built-in fonts have no glyphs for them and they render as black
boxes.

Module 23 is the regression list. When a defect is found and fixed, add it there with the
symptom that would identify a recurrence, not just the fix.
