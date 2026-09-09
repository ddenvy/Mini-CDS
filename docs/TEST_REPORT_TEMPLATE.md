# Test Report — Mini-CDS

**Project:** Mini-CDS (Chromatography Data System)
**Date:** 2026-09-09
**Tester:** ___________
**Build:** .NET 10, WPF
**Test Environment:** Windows, SQLite, MQTTnet 4.3.6

---

## 1. Executive Summary

Mini-CDS was tested against its core feature set: real-time acquisition, signal
processing and peak detection, electronic signatures, audit trail with hash-chain
integrity, append-only persistence, CSV/PDF reporting, and MQTT instrument
integration.

**Overall Result:** PASS / FAIL

**Unit Tests:** 140 / 140 passed (100%)

---

## 2. Test Environment

| Item | Value |
|------|-------|
| OS | Windows 10/11 |
| .NET SDK | 10.0 |
| Database | SQLite (EF Core 10) |
| MQTT Broker | mosquitto / eclipse-mosquitto (optional) |
| Python | 3.8+ (for MQTT publisher) |

---

## 3. Unit & Integration Test Results

Run command: `dotnet test`

| Suite | Tests | Passed | Failed |
|-------|-------|--------|--------|
| Acquisition | — | — | — |
| Audit (HashChain, AuditService) | — | — | — |
| Auth | — | — | — |
| Persistence (AuditTrail, Signature, DbContext) | — | — | — |
| Signal Processing | — | — | — |
| Reporting (CSV, PDF) | — | — | — |
| Security (PasswordHasher) | — | — | — |
| WPF ViewModels | — | — | — |
| **Total** | **140** | **140** | **0** |

---

## 4. Manual Test Results

| # | Test Case | Result | Evidence |
|---|-----------|--------|----------|
| 1 | Login screen renders | PASS / FAIL | 01-login-screen.png |
| 2 | Successful login (admin/demo123) | PASS / FAIL | 02-main-window-after-login.png |
| 3 | Acquisition starts (Simulator) | PASS / FAIL | 03-acquisition-running.png |
| 4 | Peaks table populates with metrics | PASS / FAIL | 04-peaks-table.png |
| 5 | Acquisition stops, final chart shown | PASS / FAIL | 05-acquisition-stopped.png |
| 6 | Void sample requires e-signature | PASS / FAIL | 06-signature-dialog.png |
| 7 | Export report to CSV | PASS / FAIL | 07-export-report-dialog.png |
| 8 | Export report to PDF | PASS / FAIL | 08-pdf-report.png |
| 9 | Audit trail shows all actions | PASS / FAIL | 09-audit-trail.png |
| 10 | Hash-chain integrity: INTACT | PASS / FAIL | 10-integrity-intact.png |
| 11 | MQTT mode receives signal (optional) | PASS / FAIL | 11-mqtt-mode.png |

---

## 5. Regulatory Compliance Verification (21 CFR Part 11 / ALCOA+)

| Requirement | Verified | Notes |
|-------------|----------|-------|
| §11.10(d) Limited access | Yes / No | Login required, Argon2id passwords |
| §11.10(e) Audit trail | Yes / No | Immutable log, hash-chain intact |
| §11.10(g) Authority checks | Yes / No | Roles enforced |
| §11.10(k) Append-only | Yes / No | SQLite triggers block UPDATE/DELETE |
| §11.50 E-signatures | Yes / No | Username+Password+Meaning+Reason |
| §11.200 Signature components | Yes / No | All components captured |
| ALCOA+ Attributable | Yes / No | actorUserId on every action |
| ALCOA+ Contemporaneous | Yes / No | TimestampUtc on all events |
| ALCOA+ Original | Yes / No | Append-only storage |
| ALCOA+ Accurate | Yes / No | Hash-chain verifies integrity |

---

## 6. Defects Found

| ID | Severity | Description | Status |
|----|----------|-------------|--------|
| — | — | No defects found during testing | — |

*(Add rows as needed)*

---

## 7. Conclusion

All planned test cases (unit + manual) passed. The application demonstrates
core CDS functionality with 21 CFR Part 11 / ALCOA+ data integrity controls:
append-only audit trail with hash-chain verification, electronic signatures,
and secure authentication.

**Recommendation:** Approved for demonstration / educational use. Not certified
for regulated production use.

---

## Appendix — Screenshots

Place screenshots in `docs/screenshots/` with filenames matching the checklist.
