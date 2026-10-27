"use strict";
const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const net = require("node:net");
const { spawn } = require("node:child_process");
const newman = require("newman");

const root = path.resolve(__dirname, "../..");
const project = path.join(root, "backend/Afterform.Api");
const dll = path.join(project, "bin/SecurityChecks/net10.0/Afterform.Api.dll");
const port = Number(process.argv[2] || 5019);
if (!Number.isSafeInteger(port) || port < 1024 || port > 65535 || port === 5000) {
  console.error("Use a separate local test port, for example 5019; everyday port 5000 is excluded.");
  process.exit(1);
}
const baseUrl = `http://127.0.0.1:${port}`;
const runId = `${new Date().toISOString().slice(0, 10).replaceAll("-", "")}.${crypto.randomBytes(6).toString("hex")}`;
const runPath = path.join(root, ".local/security-checks", runId);
const collectionPath = path.join(root, "postman/Afterform.Step9.postman_collection.json");
const folders = ["01 — Sessions and ownership", "02 — Upload validation", "03 — Account lockout", "04 — Login rate limit"];
let server = null;
let environment = {};
let original = null;
let stage = "initialization";
let oversizedFile = null;
const report = {
  runId, startedAtUtc: new Date().toISOString(), baseUrl, status: "inProgress",
  executor: "Newman 6.2.2 / real ASP.NET Core / existing Docker MySQL",
  phases: [], fixtures: [], setupError: null
};
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

function dotnetEnvironment() {
  return {
    ...process.env, ASPNETCORE_ENVIRONMENT: "Development", DOTNET_ENVIRONMENT: "Development",
    DOTNET_CLI_HOME: path.join(root, ".local/dotnet-home"), DOTNET_CLI_TELEMETRY_OPTOUT: "1",
    DOTNET_GENERATE_ASPNET_CERTIFICATE: "false", Logging__LogLevel__Default: "Critical"
  };
}
async function ensureUnusedPort() {
  await new Promise((resolve, reject) => {
    const probe = net.createServer();
    probe.once("error", () => reject(new Error("Test port is already in use. Choose a free port; the existing process will not be stopped.")));
    probe.listen(port, "127.0.0.1", () => probe.close(resolve));
  });
}
async function stopServer() {
  const child = server;
  server = null;
  if (!child || child.exitCode !== null || child.signalCode !== null) return;
  child.kill();
  await Promise.race([new Promise(resolve => child.once("exit", resolve)), sleep(5000)]);
  if (child.exitCode === null && child.signalCode === null) throw new Error("The owned test server could not be stopped. Close its process before rerunning.");
}
async function startServer() {
  await ensureUnusedPort();
  server = spawn("dotnet", [dll, "--urls", baseUrl], {
    cwd: project, env: dotnetEnvironment(), windowsHide: true, stdio: "ignore"
  });
  let launchError = false;
  server.once("error", () => { launchError = true; });
  for (let attempt = 0; attempt < 40; attempt++) {
    if (launchError || server.exitCode !== null) throw new Error("The test server did not start. Check .NET and local settings.");
    try {
      const response = await fetch(`${baseUrl}/api/health/live`, { signal: AbortSignal.timeout(1000) });
      if (response.status === 200) {
        const ready = await fetch(`${baseUrl}/api/health/ready`, { signal: AbortSignal.timeout(15000) });
        if (ready.status !== 200) {
          const problem = await ready.json().catch(() => null);
          const knownTitles = ["Database is unavailable.", "Database schema needs a migration.", "Database readiness check failed."];
          const reason = knownTitles.includes(problem?.title) ? ` ${problem.title}` : "";
          throw new Error(`MySQL readiness returned HTTP ${ready.status}.${reason} Start Docker, apply pending migrations, and run this script in your normal PowerShell window.`);
        }
        const data = await ready.json();
        if (data.database !== "connected" || data.schema !== "upToDate") throw new Error("Database readiness response is incomplete.");
        return;
      }
    } catch (error) {
      if (error.message.startsWith("MySQL readiness") || error.message.startsWith("Database readiness")) throw error;
    }
    await sleep(250);
  }
  throw new Error("The test server did not become ready within the startup deadline.");
}
async function provision(userName, displayName, password) {
  await new Promise((resolve, reject) => {
    const child = spawn("dotnet", [dll, "--provision-artist"], {
      cwd: project, env: dotnetEnvironment(), windowsHide: true, stdio: ["pipe", "ignore", "ignore"]
    });
    const timeout = setTimeout(() => { child.kill(); reject(new Error("QA artist provisioning timed out. Check MySQL in your normal terminal.")); }, 30000);
    child.once("error", () => { clearTimeout(timeout); reject(new Error("Could not launch the local QA account command.")); });
    child.once("exit", code => {
      clearTimeout(timeout);
      if (code === 0) resolve();
      else reject(new Error("QA artist provisioning failed. Check MySQL connectivity and applied migrations."));
    });
    child.stdin.on("error", () => {});
    // The password is sent on stdin, never in arguments, a file or a report.
    child.stdin.end(JSON.stringify({ UserName: userName, DisplayName: displayName, Password: password }) + "\n", "utf8");
  });
}
function createFixtures() {
  fs.mkdirSync(runPath, { recursive: true });
  const samples = 22050;
  const wav = Buffer.alloc(44 + samples * 2);
  wav.write("RIFF", 0); wav.writeUInt32LE(wav.length - 8, 4); wav.write("WAVEfmt ", 8);
  wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(22050, 24); wav.writeUInt32LE(44100, 28); wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
  wav.write("data", 36); wav.writeUInt32LE(samples * 2, 40);
  for (let i = 0; i < samples; i++) wav.writeInt16LE(Math.round(Math.sin(2 * Math.PI * 440 * i / 22050) * 1200), 44 + i * 2);
  original = wav;
  const paths = {
    validFile: path.join(runPath, "synthetic-demo.wav"), emptyFile: path.join(runPath, "empty.wav"),
    invalidExtensionFile: path.join(runPath, "not-audio.txt"), fakeAudioFile: path.join(runPath, "renamed-text.wav"),
    oversizedFile: path.join(runPath, "oversized.wav")
  };
  fs.writeFileSync(paths.validFile, wav);
  fs.writeFileSync(paths.emptyFile, Buffer.alloc(0));
  fs.writeFileSync(paths.invalidExtensionFile, "QA9 invalid extension fixture");
  fs.writeFileSync(paths.fakeAudioFile, "QA9 plain text; this is not WAV audio");
  oversizedFile = paths.oversizedFile;
  const fd = fs.openSync(oversizedFile, "wx");
  try {
    const size = 100 * 1024 * 1024 + 256 * 1024 + 1024;
    fs.ftruncateSync(fd, size);
    // Structurally valid PCM WAV filled with silence: without size limits,
    // the format inspector could accept it. A malformed header must not
    // accidentally make the size test pass.
    const header = Buffer.from(wav.subarray(0, 44));
    header.writeUInt32LE(size - 8, 4);
    header.writeUInt32LE(size - 44, 40);
    fs.writeSync(fd, header, 0, header.length, 0);
  }
  finally { fs.closeSync(fd); }
  return { ...paths, fileSizeBytes: String(wav.length), fileSha256: crypto.createHash("sha256").update(wav).digest("hex") };
}
function safeAssert(phase, name, passed) {
  phase.binaryChecks.push({ name, passed });
  console.log(`${passed ? "PASS" : "FAIL"}: ${name}`);
}
async function runPhase(folder) {
  const phaseReport = { name: folder, requests: [], assertions: [], binaryChecks: [], failed: 0 };
  report.phases.push(phaseReport);
  await new Promise((resolve, reject) => {
    const run = newman.run({
      collection: collectionPath, folder, ignoreRedirects: true,
      environment: { name: "In-memory QA credentials", values: Object.entries(environment).map(([key, value]) => ({ key, value, enabled: true })) },
      workingDir: runPath, insecureFileRead: false, reporters: [],
      timeout: 180000, timeoutRequest: 30000, timeoutScript: 5000
    }, (error, summary) => {
      if (summary?.environment) environment = summary.environment.toObject();
      phaseReport.failed = (summary?.run?.failures?.length || 0) + phaseReport.binaryChecks.filter(check => !check.passed).length;
      if (error) { phaseReport.failed += 1; reject(new Error("The Postman collection runner encountered an execution error.")); return; }
      if (phaseReport.failed) reject(new Error("One or more API assertions failed. Read the sanitized results by request/test name."));
      else resolve();
    });
    run.on("request", (error, args) => {
      const name = args.item.name;
      const code = args.response?.code ?? null;
      phaseReport.requests.push({ name, statusCode: code, transportError: Boolean(error) });
      console.log(`HTTP ${code ?? "ERROR"}: ${name}`);
      // Do not export Newman summaries: they contain passwords and cookies.
      if (name.includes("[SHA-A]")) {
        const stream = args.response?.stream;
        const same = code === 200 && Buffer.isBuffer(stream)
          && crypto.createHash("sha256").update(stream).digest("hex") === environment.fileSha256;
        safeAssert(phaseReport, "Downloaded bytes match the uploaded WAV SHA-256", same);
      }
      if (name.includes("[RANGE-A]")) {
        const stream = args.response?.stream;
        safeAssert(phaseReport, "Byte range matches the first 16 original bytes", code === 206 && Buffer.isBuffer(stream) && stream.equals(original.subarray(0, 16)));
      }
    });
    run.on("assertion", (error, args) => {
      phaseReport.assertions.push({ request: args.item.name, name: args.assertion, passed: !error });
      if (error) console.log(`FAIL assertion: ${args.item.name} / ${args.assertion}`);
    });
  });
}
function writeReport() {
  report.finishedAtUtc = new Date().toISOString();
  report.requestCount = report.phases.reduce((sum, phase) => sum + phase.requests.length, 0);
  report.assertionCount = report.phases.reduce((sum, phase) => sum + phase.assertions.length + phase.binaryChecks.length, 0);
  report.failedCount = report.phases.reduce((sum, phase) => sum + phase.failed, 0);
  report.demoIds = [environment.demoIdA, environment.demoIdB, environment.forgedDemoIdB].filter(Boolean);
  fs.mkdirSync(runPath, { recursive: true });
  const json = JSON.stringify(report, null, 2) + "\n";
  fs.writeFileSync(path.join(runPath, "result.json"), json);
  fs.writeFileSync(path.join(root, ".local/security-checks/latest-result.json"), json);
  const lines = ["# AFTER/FORM — 9. adım çalıştırma sonucu", "", `- Durum: **${report.status}**`,
    `- Başlangıç (UTC): ${report.startedAtUtc}`, `- Yerel test adresi: ${baseUrl}`,
    `- Postman senaryolarındaki HTTP isteği: ${report.requestCount}`, `- Kontrol: ${report.assertionCount}`, `- Başarısızlık: ${report.failedCount}`,
    `- Kurulum hatası: ${report.setupError || "Yok"}`, "", "## Senaryolar", "", "| Grup | İstek | Kontrol | Başarısız |", "|---|---:|---:|---:|"];
  for (const phase of report.phases) lines.push(`| ${phase.name} | ${phase.requests.length} | ${phase.assertions.length + phase.binaryChecks.length} | ${phase.failed} |`);
  const failures = report.phases.flatMap(phase => [
    ...phase.assertions.filter(check => !check.passed).map(check => `${check.request}: ${check.name}`),
    ...phase.binaryChecks.filter(check => !check.passed).map(check => check.name),
    ...phase.requests.filter(req => req.transportError).map(req => `${req.name}: transport error`)
  ]);
  if (failures.length) lines.push("", "## Başarısız kontroller", "", ...failures.map(name => `- ${name}`));
  lines.push("", "## Deneme verileri", "", ...report.fixtures.map(name => `- Kullanıcı: ${name}`),
    `- Gönderim kimlikleri: ${report.demoIds.join(", ") || "Henüz yok"}`,
    "", "Bu rapor parola, oturum çerezi, CSRF token, istek gövdesi veya tam Newman çıktısı içermez.",
    report.fixtures.length ? "Oluşturulmuş QA hesapları ve başarılı yapay demo kayıtları incelemek için tutulur. B hesabı kilitlenme grubu başarılıysa 15 dakika kilitlidir." : "Bu koşuda QA hesabı veya demo oluşturulmadı.",
    "", "Bu senaryoların geçmesi bütün güvenlik açıklarının bulunmadığı anlamına gelmez. Üretim HTTPS, çok sunuculu rate limit ve tam dosya/zararlı yazılım analizi bu koşuda doğrulanmaz.");
  fs.writeFileSync(path.join(root, "docs/09-last-run.md"), lines.join("\n") + "\n");
  console.log(`RESULT: ${report.status} | requests=${report.requestCount} | checks=${report.assertionCount} | failures=${report.failedCount}`);
  console.log(`Report: ${path.join(root, "docs/09-last-run.md")}`);
}

(async () => {
  try {
    if (!fs.existsSync(dll)) throw new Error("Build the SecurityChecks configuration with scripts/Test-ArtistSecurity.ps1 first.");
    if (!fs.existsSync(path.join(root, ".local/backend.settings.json"))) throw new Error("Local MySQL settings are missing. Complete database setup first.");
    stage = "database readiness";
    console.log("[1/5] Starting a separate local test server and checking MySQL.");
    await startServer();
    stage = "QA artist provisioning";
    console.log("[2/5] Creating two separate QA artists. Credentials stay in memory.");
    const password = crypto.randomBytes(24).toString("base64url");
    const userNameA = `qa9.a.${runId}`;
    const userNameB = `qa9.b.${runId}`;
    await provision(userNameA, "QA9 Artist A", password);
    report.fixtures.push(userNameA);
    await provision(userNameB, "QA9 Artist B", password);
    report.fixtures.push(userNameB);
    stage = "synthetic file preparation";
    console.log("[3/5] Preparing one-second WAV and invalid file fixtures.");
    environment = {
      baseUrl, userNameA, passwordA: password, userNameB, passwordB: password,
      unknownUser: `qa9.missing.${runId}`, trackTitleA: `QA9 ${runId} A recording`, trackTitleB: `QA9 ${runId} B recording`,
      searchMarker: runId, ...createFixtures()
    };
    console.log("[4/5] Running four Postman folders with fresh process rate counters.");
    for (let index = 0; index < folders.length; index++) {
      if (index > 0) { await stopServer(); await startServer(); }
      stage = folders[index];
      console.log(`FOLDER: ${stage}`);
      await runPhase(folders[index]);
    }
    report.status = "passed";
  } catch (error) {
    report.status = report.phases.length ? "failed" : "blocked";
    report.setupError = `${stage}: ${error.message}`;
    console.error(report.setupError);
    process.exitCode = 1;
  } finally {
    try { await stopServer(); } catch { report.status = "failed"; report.setupError = "Owned test server cleanup failed."; process.exitCode = 1; }
    // Remove only the generated large input; no recursive deletion or DB cleanup.
    if (oversizedFile && path.dirname(path.resolve(oversizedFile)) === path.resolve(runPath)
      && path.basename(oversizedFile) === "oversized.wav") {
      try { fs.unlinkSync(oversizedFile); } catch { }
    }
    console.log("[5/5] Writing credential-free results; stopping only the owned test server.");
    writeReport();
    environment = {};
  }
})();
