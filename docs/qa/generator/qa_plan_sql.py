# -*- coding: utf-8 -*-
"""Shared reference material: the SQL cookbook and the log lines worth grepping.

Lives here rather than inside either generator so the PDF and the spreadsheet cannot drift
apart - the same list feeds both.
"""

SQL_COOKBOOK = [
    ("Is anything actually mapping?",
     "select c.\"Name\", count(*) tickets, count(*) filter (where t.\"PsaStatus\"=t.\"PortalStatus\") "
     "status_raw, count(*) filter (where t.\"PsaPriority\"=t.\"PortalPriority\") priority_raw "
     "from tickets t join psa_connections c on c.\"Id\"=t.\"PsaConnectionId\" group by 1;"),
    ("Date coverage",
     "select c.\"Name\", count(*) total, count(t.\"PsaCreatedAt\") raised, count(t.\"SlaDueAt\") "
     "sla, count(t.\"ClosedAt\") closed from tickets t join psa_connections c "
     "on c.\"Id\"=t.\"PsaConnectionId\" group by 1;"),
    ("Inbound mapping ambiguity (must return 0 rows)",
     "select \"PsaConnectionId\",\"PortalField\",\"ExternalValue\",count(*) from field_mappings "
     "where \"IsActive\" and \"Direction\" in (2,3) group by 1,2,3 having count(*)>1;"),
    ("Outbound mapping ambiguity (must return 0 rows)",
     "select \"PsaConnectionId\",\"PortalField\",\"PortalValue\",count(*) from field_mappings "
     "where \"IsActive\" and \"Direction\" in (1,3) group by 1,2,3 having count(*)>1;"),
    ("Duplicate tickets after a paginated import (must return 0 rows)",
     "select \"PsaConnectionId\",\"ExternalTicketId\",count(*) from tickets group by 1,2 "
     "having count(*)>1;"),
    ("Connection health",
     "select \"Name\",\"Status\",\"IsEnabled\",\"LastSuccessfulSyncAt\",\"LastError\" "
     "from psa_connections;"),
    ("Force a full re-sync of everything",
     "update psa_connections set \"LastSuccessfulSyncAt\"=NULL;"),
    ("Activity rollup state",
     "select (select count(*) from activity_events) events, (select count(*) from "
     "activity_daily_facts) facts, (select sum(\"EventCount\") from activity_daily_facts) "
     "rolled_up;"),
    ("Events by the day they happened",
     "select \"OccurredAt\"::date d, count(*) from activity_events group by 1 order by 1;"),
    ("Open-ticket count as the client report computes it",
     "select count(*) from tickets where \"PortalStatus\" in "
     "('NEW','IN_PROGRESS','WAITING_CUSTOMER','ON_HOLD');"),
    ("All mapping rules for one connection",
     "select \"PortalField\",\"PortalValue\",\"ExternalValue\",\"Direction\",\"IsActive\" "
     "from field_mappings where \"PsaConnectionId\"='<id>' order by 1,2;"),
]

LOG_LINES = [
    ("No mapping rule matches", "A provider value nothing maps. Once per sync run."),
    ("Scheduled sync failed", "A connection's sync threw. The exception follows in @x."),
    ("safety cap", "The run stopped at 50 pages with more to read - the import is incomplete."),
    ("Activity rollup:", "Days recomputed, facts written, raw events expired."),
    ("picklist", "Autotask option ids and labels - use it to read a rule that holds a bare id."),
    ("ConnectWise ticket fields", "Which fields the provider actually sent, unioned across the "
                                  "page. Field names only."),
]

# How to reach the database and the log at all.
ACCESS = [
    ("Database",
     "docker exec desk-portal-prod-postgres-1 psql -U desk -d desk_portal -c \"<query>\""),
    ("Worker log",
     "docker logs desk-portal-prod-worker-1 --since 30m"),
    ("Unit suite",
     "dotnet test tests/unit"),
]
