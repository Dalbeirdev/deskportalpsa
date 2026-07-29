import os
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, PageBreak, ListFlowable, ListItem, Table, TableStyle, HRFlowable
)

OUT = r"E:\autotask\DPI-Autotask\desk-portal\apps\web\public\user-guide.pdf"
os.makedirs(os.path.dirname(OUT), exist_ok=True)

BRAND = colors.HexColor("#2563eb")
INK = colors.HexColor("#0f172a")
MUTED = colors.HexColor("#475569")
FAINT = colors.HexColor("#64748b")
LINE = colors.HexColor("#e2e8f0")

styles = getSampleStyleSheet()

def S(name, **kw):
    return ParagraphStyle(name, parent=styles["Normal"], **kw)

title_s   = S("t", fontName="Helvetica-Bold", fontSize=34, textColor=BRAND, leading=38, spaceAfter=6)
sub_s     = S("sub", fontName="Helvetica", fontSize=13, textColor=MUTED, leading=18, spaceAfter=2)
h1_s      = S("h1", fontName="Helvetica-Bold", fontSize=17, textColor=INK, leading=21, spaceBefore=16, spaceAfter=6)
h2_s      = S("h2", fontName="Helvetica-Bold", fontSize=12.5, textColor=INK, leading=16, spaceBefore=10, spaceAfter=3)
body_s    = S("b", fontName="Helvetica", fontSize=10.5, textColor=INK, leading=15.5, spaceAfter=6, alignment=TA_LEFT)
muted_s   = S("m", fontName="Helvetica", fontSize=9.5, textColor=MUTED, leading=13, spaceAfter=4)
bullet_s  = S("bl", fontName="Helvetica", fontSize=10.5, textColor=INK, leading=15)
eyebrow_s = S("ey", fontName="Helvetica-Bold", fontSize=9, textColor=BRAND, leading=12, spaceAfter=2)
note_s    = S("n", fontName="Helvetica", fontSize=9.5, textColor=MUTED, leading=13, spaceAfter=4,
              backColor=colors.HexColor("#f1f5f9"), borderPadding=6, leftIndent=2, rightIndent=2)

def bullets(items):
    return ListFlowable(
        [ListItem(Paragraph(t, bullet_s), leftIndent=10, value="•") for t in items],
        bulletType="bullet", start="•", leftIndent=14, spaceAfter=6,
    )

def hr():
    return HRFlowable(width="100%", thickness=0.7, color=LINE, spaceBefore=4, spaceAfter=8)

story = []

# ---- Cover ----
story += [Spacer(1, 40*mm)]
story += [Paragraph("DESK PORTAL", eyebrow_s)]
story += [Paragraph("User Guide", title_s)]
story += [Paragraph("A multi-tenant PSA ticket portal — for clients, technicians, managers, and administrators.", sub_s)]
story += [Spacer(1, 6)]
story += [hr()]
story += [Paragraph("Version 0.1.0", muted_s)]
story += [Paragraph("This guide covers everyday use of the portal: raising and following tickets, the productivity "
                    "dashboards, and administering PSA connections and field mappings.", body_s)]
story += [PageBreak()]

# ---- 1. Getting started ----
story += [Paragraph("1. Getting started", h1_s), hr()]
story += [Paragraph("Signing in", h2_s)]
story += [Paragraph("Open the portal and choose <b>Continue with SSO</b>. Authentication is handled by your "
                    "organization's identity provider — you'll be returned to the dashboard once signed in. Your name "
                    "and a <b>Sign out</b> option appear at the top right.", body_s)]
story += [Paragraph("Finding your way around", h2_s)]
story += [Paragraph("The left sidebar is your main navigation. On a phone it collapses into a scrollable bar at the top. "
                    "A light/dark theme toggle sits at the top right.", body_s)]
story += [bullets([
    "<b>Tickets</b> — raise and follow your support requests.",
    "<b>Productivity</b> — technician &amp; team performance (staff).",
    "<b>PSA Connections, Field Mapping, Integration Health, Background Jobs, Audit Log</b> — administration.",
])]

# ---- 2. For clients ----
story += [Paragraph("2. Raising &amp; following tickets", h1_s), hr()]
story += [Paragraph("Create a ticket", h2_s)]
story += [Paragraph("Go to <b>Tickets → New ticket</b>. Give it a short title, choose a priority, and describe the "
                    "issue. On submit, the ticket is created in your provider's system (the PSA) and appears in your list.", body_s)]
story += [Paragraph("Track progress", h2_s)]
story += [bullets([
    "The <b>ticket list</b> shows status, priority, queue and when each was raised.",
    "Open a ticket to see its <b>public conversation</b> and add a reply.",
    "<b>Attach files</b> from the ticket detail — they are scanned for malware; executables are blocked; 25 MB max. "
    "Only clean files can be downloaded.",
    "<b>Notifications</b> lists recent activity on your tickets.",
])]
story += [Paragraph("Internal notes your support team writes are never shown in the portal — you only ever see public "
                    "replies.", note_s)]

# ---- 3. Dashboards ----
story += [Paragraph("3. Productivity dashboards (staff)", h1_s), hr()]
story += [Paragraph("The <b>Productivity</b> page shows technician and team metrics: assigned, resolved, open and "
                    "overdue tickets, SLA compliance, average resolution time, and time worked. A configurable "
                    "<b>productivity score</b> combines several signals into a single number, with a breakdown per "
                    "component. Use <b>Export CSV</b> to download the team view.", body_s)]
story += [Paragraph("Productivity scores are operational indicators only and must not be used as the sole basis for "
                    "employee performance decisions. The score only counts signals that are actually measured, and "
                    "reports how much of the model that covers.", note_s)]

story += [PageBreak()]

# ---- 4. Administration ----
story += [Paragraph("4. Administration", h1_s), hr()]

story += [Paragraph("PSA Connections", h2_s)]
story += [Paragraph("Connect Autotask and ConnectWise tenants under <b>PSA Connections</b>.", body_s)]
story += [bullets([
    "<b>Add connection</b> — name it, choose the provider, enter the API endpoint and credentials. "
    "Credentials are stored in a secure secret vault, never in the database and never shown again.",
    "<b>Edit</b> — change settings; leave the credential fields blank to keep the existing keys, or enter new ones to rotate.",
    "<b>Test</b> — runs a live check against the PSA and updates the connection's health status.",
    "<b>Boards</b> — discovers the connection's service boards/queues, statuses, priorities and categories live.",
])]

story += [Paragraph("Field Mapping", h2_s)]
story += [Paragraph("Under <b>Field Mapping</b>, translate the portal's neutral values to each PSA's real values — this "
                    "is how the portal and your PSA agree on what a status, priority, queue or category means.", body_s)]
story += [bullets([
    "Pick a <b>connection</b> and a <b>field</b> (Status, Priority, Queue/Board, Category).",
    "For status and priority, map each portal value to a discovered PSA value.",
    "For queues and categories, add a mapping by naming a portal value and picking the PSA value.",
    "Each save is stored as a new <b>version</b> and recorded in the audit log, so changes can be reviewed.",
])]

story += [Paragraph("Monitoring &amp; audit", h2_s)]
story += [bullets([
    "<b>Integration Health</b> — per-connection status, pending jobs, dead-lettered jobs and failed events.",
    "<b>Background Jobs</b> — monitor sync jobs and <b>reprocess</b> any that dead-lettered.",
    "<b>Audit Log</b> — an immutable record of administrative and security events (connection changes, mapping "
    "updates, tests, job reprocessing).",
])]

# ---- 5. Security ----
story += [Paragraph("5. Security &amp; your data", h1_s), hr()]
story += [bullets([
    "Each organization's data is fully isolated — you only ever see your own.",
    "PSA credentials live only in the secret vault; they are never returned to the browser or written to logs or the audit trail.",
    "Access is governed by role-based permissions (client user, client administrator, technician, manager, administrator, auditor).",
    "Attachments are validated and malware-scanned; downloads use short-lived, signed links.",
])]

# ---- 6. Help ----
story += [Paragraph("6. Getting help", h1_s), hr()]
story += [Paragraph("If a page shows a &ldquo;sign in&rdquo; or empty state where you expect data, your session may have "
                    "expired — sign in again. For anything else, contact your MSP administrator, who can review the "
                    "audit log and integration health to diagnose issues.", body_s)]

# ---- footer with page numbers ----
def footer(canvas, doc):
    canvas.saveState()
    canvas.setStrokeColor(LINE)
    canvas.setLineWidth(0.6)
    canvas.line(20*mm, 15*mm, 190*mm, 15*mm)
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(FAINT)
    canvas.drawString(20*mm, 10*mm, "Desk Portal — User Guide")
    canvas.drawRightString(190*mm, 10*mm, "Page %d" % doc.page)
    canvas.restoreState()

doc = SimpleDocTemplate(OUT, pagesize=A4,
                        leftMargin=20*mm, rightMargin=20*mm, topMargin=20*mm, bottomMargin=22*mm,
                        title="Desk Portal — User Guide", author="Desk Portal")
doc.build(story, onFirstPage=footer, onLaterPages=footer)
print("wrote", OUT)
