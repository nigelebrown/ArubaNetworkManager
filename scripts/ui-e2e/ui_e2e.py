"""Browser end-to-end test of the JCF Network Manager demo (Blazor Server, real circuit, real Chrome).

Walks the main operator journeys through the UI only, switching demo personas to prove
separation of duties. Writes ui_e2e_results.json and screenshots to the output directory.
"""
import asyncio
import json
import os
import sys
import time
from pathlib import Path

from playwright.async_api import async_playwright

BASE = "http://127.0.0.1:5180"
OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "ui-e2e-output")
OUT.mkdir(parents=True, exist_ok=True)
results = []


def record(name, ok, detail=""):
    results.append({"step": name, "outcome": "Passed" if ok else "Failed", "detail": detail})
    print(("PASS " if ok else "FAIL ") + name + ("" if ok else "  -> " + detail))


async def open_page(pg, path, persona=None):
    if persona:
        await pg.goto(f"{BASE}/demo/persona/{persona}?returnUrl={path}")
    else:
        await pg.goto(BASE + path)
    # Wait until the interactive circuit is attached before clicking anything.
    await pg.wait_for_function("() => typeof Blazor !== 'undefined'")
    await pg.wait_for_timeout(1200)


async def feedback(pg):
    loc = pg.locator(".feedback")
    try:
        await loc.first.wait_for(timeout=8000)
        return (await loc.first.inner_text()).strip()
    except Exception:
        return ""


async def main():
    async with async_playwright() as p:
        browser = await p.chromium.launch(executable_path=os.environ.get("CHROME_PATH", "/usr/bin/google-chrome"), args=["--no-sandbox"])
        ctx = await browser.new_context(viewport={"width": 1440, "height": 1000})
        pg = await ctx.new_page()
        console_errors = []
        pg.on("pageerror", lambda e: console_errors.append(str(e)))
        pg.on("console", lambda m: console_errors.append("console: " + m.text) if m.type == "error" else None)
        pg.on("response", lambda r: console_errors.append(f"HTTP {r.status} {r.url}") if r.status >= 500 else None)

        # 1. Every navigation page renders interactively without the error UI.
        await open_page(pg, "/", "admin")
        nav = await pg.locator("nav a").evaluate_all("els => els.map(e => e.getAttribute('href'))")
        bad = []
        for href in [h for h in nav if h and h.startswith("/") and not h.startswith("/account")]:
            await open_page(pg, href)
            h1 = await pg.locator("h1").first.inner_text() if await pg.locator("h1").count() else ""
            if not h1 or await pg.locator("#blazor-error-ui").is_visible() or "Page not found" in await pg.inner_text("main"):
                bad.append(href)
        record("All navigation pages render with an interactive circuit", not bad, ",".join(bad))
        await open_page(pg, "/")
        await pg.screenshot(path=str(OUT / "01_overview.png"), full_page=True)

        # 2. Register a device through the UI (operator).
        await open_page(pg, "/devices", "operator")
        await pg.get_by_label("Display name").fill("UI Test AP-535")
        await pg.get_by_label("Equipment type").select_option("Ap535")
        await pg.get_by_label("Management IP").fill("192.0.2.95")
        await pg.get_by_label("Secret reference (not password)").fill("LAB_AP_UI")
        await pg.get_by_role("button", name="Register device").click()
        msg = await feedback(pg)
        record("Operator registers an AP-535 via the UI", "registered" in msg.lower(), msg)
        rows = await pg.locator("table tbody tr").all_inner_texts()
        record("New device appears in the inventory table", any("UI Test AP-535" in r for r in rows), str(rows)[:200])

        # 2b. Out-of-policy IP is refused with a clear message.
        await pg.get_by_label("Display name").fill("Rogue")
        await pg.get_by_label("Management IP").fill("8.8.8.8")
        await pg.get_by_label("Secret reference (not password)").fill("X")
        await pg.get_by_role("button", name="Register device").click()
        await pg.wait_for_timeout(800)
        msg = await feedback(pg)
        record("Public IP outside management CIDRs is refused", "approved management subnet" in msg, msg)

        # 3. Read-only probe of the demo AP.
        row = pg.locator("tr", has_text="Demo AP-505")
        await row.get_by_role("button", name="Read APIs").click()
        await pg.wait_for_timeout(1500)
        msg = await feedback(pg)
        record("Read-only probe authenticates (simulated) and records telemetry", "authenticated" in msg.lower(), msg)
        await pg.screenshot(path=str(OUT / "02_devices_probe.png"), full_page=True)

        # 4. Analyze Now raises the 80 MHz policy-drift recommendation.
        await open_page(pg, "/recommendations")
        await pg.get_by_role("button", name="Analyze Now").click()
        msg = await feedback(pg)
        record("Analyze Now completes with findings and no device change", "Analysis completed" in msg and "No device configuration changed" in msg, msg)
        text = await pg.inner_text("main")
        record("80 MHz policy drift recommendation is listed", "80 MHz policy drift" in text, "")
        record("Operator who raised it cannot review it (no review buttons)", await pg.get_by_role("button", name="Accept").count() == 0, "")

        # 5. Independent approver accepts it.
        await open_page(pg, "/recommendations", "reviewer-one")
        acc = pg.get_by_role("button", name="Accept")
        has = await acc.count() > 0
        if has:
            await acc.first.click()
            msg = await feedback(pg)
        else:
            msg = "no Accept button"
        record("Independent approver accepts the recommendation", has and "approved" in msg.lower(), msg)
        await pg.screenshot(path=str(OUT / "03_recommendations.png"), full_page=True)

        # 6. Operator submits an RF (band steering) proposal - requires TWO approvers.
        await open_page(pg, "/configuration", "operator")
        target = await pg.locator("select").first.locator("option", has_text="Demo AP-505").get_attribute("value")
        await pg.locator("select").first.select_option(target)
        await pg.get_by_label("Action", exact=False).first.select_option("ChangeBandSteering")
        await pg.wait_for_timeout(500)
        await pg.get_by_label("Band steering", exact=False).last.select_option("balance-bands")
        await pg.get_by_label("Justification").fill("UI E2E: balance bands in ICT lab")
        await pg.get_by_role("button", name="Submit for independent approval").click()
        msg = await feedback(pg)
        record("Operator submits an ARM band-steering proposal", "submitted" in msg.lower(), msg)

        # 6b. Operator cannot approve own proposal.
        await open_page(pg, "/changes")
        await pg.get_by_role("button", name="Approve").first.click()
        msg = await feedback(pg)
        record("Requester is blocked from approving their own proposal", "ChangeApprover role required" in msg or "different authorized person" in msg, msg)

        # 7. First approver -> PendingSecondApproval; same approver again is refused.
        await open_page(pg, "/changes", "reviewer-one")
        await pg.get_by_role("button", name="Approve").first.click()
        await feedback(pg)
        await pg.wait_for_timeout(500)
        state = await pg.locator("tbody tr").first.locator(".chip").inner_text()
        record("First approval moves RF proposal to PendingSecondApproval", state == "PendingSecondApproval", state)
        await pg.get_by_role("button", name="Approve").first.click()
        await pg.wait_for_timeout(800)
        msg = await feedback(pg)
        record("Same approver cannot give the second approval", "different authorized person" in msg, msg)

        # 8. Second, different approver -> Approved.
        await open_page(pg, "/changes", "reviewer-two")
        await pg.get_by_role("button", name="Approve").first.click()
        await feedback(pg)
        await pg.wait_for_timeout(500)
        state = await pg.locator("tbody tr").first.locator(".chip").inner_text()
        record("Second independent approval moves proposal to Approved", state == "Approved", state)
        record("Approver without operator role sees Execute but is refused", True, "checked below")
        if await pg.get_by_role("button", name="Execute approved change").count():
            await pg.get_by_role("button", name="Execute approved change").first.click()
            await pg.wait_for_timeout(800)
            msg = await feedback(pg)
            results[-1].update({"outcome": "Passed" if "role required" in msg.lower() else "Failed", "detail": msg})

        # 9. Operator executes (demo simulation) -> Applied with execution evidence.
        await open_page(pg, "/changes", "operator")
        await pg.get_by_role("button", name="Execute approved change").first.click()
        msg = await feedback(pg)
        record("Operator executes the approved change (simulated) -> Applied", "Applied" in msg and "SIMULATION" in msg, msg)
        hist = await pg.locator("section", has_text="Execution and recovery history").inner_text()
        record("Execution history shows preflight and verification events", "Preflight" in hist and "Execution/verification" in hist, "")
        await pg.screenshot(path=str(OUT / "04_change_board.png"), full_page=True)

        # 10. Compliance evaluation on the Operations page.
        await open_page(pg, "/operations")
        await pg.get_by_role("button", name="Evaluate observed configuration").click()
        msg = await feedback(pg)
        record("Operator runs compliance evaluation (read-only)", "zero configuration writes" in msg, msg)
        text = await pg.inner_text("main")
        record("80 MHz advertised finding is shown", "EIGHTY_MHZ_ADVERTISED" in text, "")

        # 11. Read-only viewer cannot register devices or run analysis.
        await open_page(pg, "/devices", "viewer")
        await pg.get_by_label("Display name").fill("Viewer attempt")
        await pg.get_by_label("Management IP").fill("192.0.2.96")
        await pg.get_by_label("Secret reference (not password)").fill("V")
        await pg.get_by_role("button", name="Register device").click()
        msg = await feedback(pg)
        record("Viewer is refused when registering a device", "role required" in msg.lower(), msg)
        await open_page(pg, "/recommendations")
        await pg.get_by_role("button", name="Analyze Now").click()
        msg = await feedback(pg)
        record("Viewer is refused when running analysis", "role required" in msg.lower(), msg)

        # 12. Capability administration is restricted at the HTTP boundary.
        # Do not navigate the browser to a page that intentionally returns 403.
        await open_page(pg, "/devices", "operator")
        restricted = await pg.request.get(BASE + "/capabilities")
        record("Operator cannot certify device capabilities", restricted.status == 403,
               f"HTTP {restricted.status}")

        # 13. Audit trail captured every action with the real actor names.
        await open_page(pg, "/audit", "admin")
        text = await pg.inner_text("main")
        needed = ["demo-operator", "demo-reviewer-one", "demo-reviewer-two", "Execute approved change", "Recommendation decision", "Register device"]
        missing = [n for n in needed if n not in text]
        record("Audit trail records actions with the real persona identities", not missing, "missing: " + ",".join(missing))
        await pg.screenshot(path=str(OUT / "05_audit.png"), full_page=True)

        hdr = (await pg.request.get(BASE + "/")).headers
        record("Security headers present (CSP, frame-ancestors, nosniff)", "frame-ancestors 'none'" in hdr.get("content-security-policy", "") and hdr.get("x-content-type-options") == "nosniff", str({k: v for k, v in hdr.items() if k.startswith(("content-security", "x-"))}))
        record("No browser console errors, CSP violations or HTTP 5xx during the run", not console_errors, "; ".join(console_errors[:5]))
        await browser.close()

    passed = sum(r["outcome"] == "Passed" for r in results)
    print(f"\nUI E2E: {passed}/{len(results)} passed")
    (OUT / "ui_e2e_results.json").write_text(json.dumps({"passed": passed, "total": len(results), "steps": results,
                                                          "ranUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())}, indent=2))
    return 0 if passed == len(results) else 1

sys.exit(asyncio.run(main()))
