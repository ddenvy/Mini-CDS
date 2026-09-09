# Testing Checklist — Mini-CDS

## Prerequisites
- .NET 10 SDK installed
- Windows OS (WPF)
- App runs: `dotnet run --project src/MiniCds.Wpf`
- Demo credentials: `admin` / `demo123`

---

## Test 1 — Login Screen

**Steps:**
1. Start the application.
2. Login window appears.

**Expected:**
- Window title "Mini-CDS — Login".
- Username and Password fields.
- Login button.

**Screenshot:** `01-login-screen.png`

---

## Test 2 — Successful Login

**Steps:**
1. Enter `admin` in Username.
2. Enter `demo123` in Password.
3. Click "Login".

**Expected:**
- Login window closes.
- Main window opens with LiveChart.

**Screenshot:** `02-main-window-after-login.png`

---

## Test 3 — Start Acquisition (Simulator)

**Steps:**
1. Main window is open.
2. Click "Start" button.

**Expected:**
- Status changes to "Running".
- Chromatogram line starts drawing on the canvas.
- Peaks table begins populating as peaks are detected.

**Screenshot (during run):** `03-acquisition-running.png`

---

## Test 4 — Peaks Table

**Steps:**
1. Wait for acquisition to run ~15–20 seconds.
2. Observe the "Detected Peaks" grid below the chart.

**Expected:**
- Columns: #, RT (s), Height, Area, FWHM (s), Plates, Tailing.
- Rows appear as peaks are detected.
- Values are numeric and formatted.

**Screenshot:** `04-peaks-table.png`

---

## Test 5 — Stop Acquisition

**Steps:**
1. Click "Stop" button.

**Expected:**
- Acquisition stops.
- Status changes to "Stopped" or "Completed".
- Final chromatogram is visible.

**Screenshot:** `05-acquisition-stopped.png`

---

## Test 6 — Void Sample (Electronic Signature)

**Steps:**
1. Select a sample from the samples list (if available) or ensure one is selected.
2. Click "Void Sample" button.
3. Signature dialog opens.

**Expected:**
- Dialog with Username, Password, Meaning (dropdown), Reason fields.
- Meaning options: Approved, Reviewed, Authorized, Rejected.

**Screenshot:** `06-signature-dialog.png`

**Continue:**
4. Enter `admin` / `demo123`.
5. Select Meaning = "Rejected".
6. Enter Reason (e.g. "Contaminated sample").
7. Click "Sign".

**Expected:**
- Dialog closes.
- Sample status changes to Voided.
- Audit entry is recorded.

---

## Test 7 — Export Report (CSV)

**Steps:**
1. Menu File → Export Report...
2. Report dialog opens.
3. Select a sample.
4. Choose Format = CSV.
5. Enter a title.
6. Click Export, choose save path.

**Expected:**
- CSV file is created.
- Contains sample info and peak data.

**Screenshot:** `07-export-report-dialog.png`

---

## Test 8 — Export Report (PDF)

**Steps:**
1. Same as Test 7 but Format = PDF.

**Expected:**
- PDF file is created with chromatogram summary and peak table.

**Screenshot:** `08-pdf-report.png` (open the generated PDF)

---

## Test 9 — Audit Trail

**Steps:**
1. Menu View → Audit Trail.
2. Audit window opens.

**Expected:**
- DataGrid with columns: ID, Timestamp, User, Action, Entity, Entity ID, Reason, Signature.
- Rows for Login, Sample actions, Signature, etc.

**Screenshot:** `09-audit-trail.png`

---

## Test 10 — Verify Integrity (Hash Chain)

**Steps:**
1. In the Audit Trail window, click "Verify Integrity".

**Expected:**
- Text "Chain integrity: INTACT" in green.

**Screenshot:** `10-integrity-intact.png`

---

## Test 11 — MQTT Mode (optional, requires broker)

**Setup:**
1. Install mosquitto or run `docker run -p 1883:1883 eclipse-mosquitto`.
2. In `appsettings.json`, set `"Instrument:Mode": "Mqtt"`.
3. Restart the app.

**Steps:**
1. Start acquisition.
2. Run publisher:
   ```bash
   cd tools/mqtt-publisher
   pip install -r requirements.txt
   python publish_chromatogram.py --broker localhost --port 1883 --device device-001
   ```

**Expected:**
- Chromatogram is drawn from MQTT data.
- Peaks detected as in Simulator mode.

**Screenshot:** `11-mqtt-mode.png`

---

## Summary of Required Screenshots

| # | File | Description |
|---|------|-------------|
| 1 | 01-login-screen.png | Login window |
| 2 | 02-main-window-after-login.png | Main window after login |
| 3 | 03-acquisition-running.png | Live chart during acquisition |
| 4 | 04-peaks-table.png | Detected peaks table |
| 5 | 05-acquisition-stopped.png | Final chromatogram after stop |
| 6 | 06-signature-dialog.png | Electronic signature dialog |
| 7 | 07-export-report-dialog.png | Report export dialog |
| 8 | 08-pdf-report.png | Generated PDF report |
| 9 | 09-audit-trail.png | Audit trail window |
| 10 | 10-integrity-intact.png | Hash-chain integrity check passed |
| 11 | 11-mqtt-mode.png | (optional) MQTT acquisition mode |
