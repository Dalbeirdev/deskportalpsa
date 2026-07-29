// Desk Portal load smoke test (k6). Thresholds encode the spec's performance targets.
// Run against a live stack:  k6 run -e BASE=http://localhost:5080 -e TOKEN=<jwt> tests/load/k6-smoke.js
//
// Requires the API + Postgres + Keycloak running (see infrastructure/docker/docker-compose.yml)
// and a valid client token. Not executable in a stack-less environment.

import http from 'k6/http';
import { check, group } from 'k6';

const BASE = __ENV.BASE || 'http://localhost:5080';
const TOKEN = __ENV.TOKEN || '';
const headers = { Authorization: `Bearer ${TOKEN}`, 'Content-Type': 'application/json' };

export const options = {
  scenarios: {
    steady: { executor: 'ramping-vus', startVUs: 0, stages: [
      { duration: '30s', target: 25 },
      { duration: '1m', target: 25 },
      { duration: '30s', target: 0 },
    ] },
  },
  thresholds: {
    // Spec §13 performance targets.
    'http_req_duration{name:health}': ['p(95)<500'],      // cached/health < 500ms
    'http_req_duration{name:ticket_list}': ['p(95)<2000'], // ticket list < 2s
    'http_req_duration{name:dashboard}': ['p(95)<3000'],   // dashboard < 3s
    'http_req_duration{name:ticket_create}': ['p(95)<3000'], // create ack < 3s
    'http_req_duration{name:webhook}': ['p(95)<10000'],    // webhook processing < 10s
    http_req_failed: ['rate<0.01'],                         // < 1% errors
  },
};

export default function () {
  group('reads', () => {
    check(http.get(`${BASE}/health`, { tags: { name: 'health' } }), { 'health 200': (r) => r.status === 200 });
    check(http.get(`${BASE}/api/tickets`, { headers, tags: { name: 'ticket_list' } }), { 'list ok': (r) => r.status === 200 });
    check(http.get(`${BASE}/api/dashboard/team`, { headers, tags: { name: 'dashboard' } }), { 'dash ok': (r) => r.status === 200 || r.status === 403 });
  });

  group('writes', () => {
    const body = JSON.stringify({ title: 'Load test ticket', priority: 'NORMAL' });
    check(http.post(`${BASE}/api/tickets`, body, { headers, tags: { name: 'ticket_create' } }),
      { 'create ok': (r) => r.status === 201 || r.status === 403 });
  });
}
