# Master test plan generator

Builds `../desk-portal-master-test-plan.pdf` — 238 test cases across 23 modules covering
authentication, tenant isolation, roles and permissions, technician registration, PSA
connections, field mapping, the sync engine, ticket lifecycle, time tracking, analytics, the
client control panel and operations.

## Regenerate

```bash
pip install reportlab
python build_qa_pdf.py
```

That writes the PDF one directory up, over the committed copy. The build is deterministic
(`invariant=1`), so rebuilding without changing a case produces a byte-identical file and no
git diff — a diff on that PDF means the plan actually changed.

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
