#!/usr/bin/env node
// SOL-3: BASE AURORA — descarga de registros HISTÓRICOS de clima espacial (NASA DONKI).
//
// La clave se lee SOLO de la variable de entorno NASA_API_KEY o del archivo local .env.local
// (excluido de git). La clave nunca se escribe en el JSON de salida ni se muestra en los registros.
//
// Uso:
//   node scripts/fetch-nasa-data.mjs                         (últimos 60 días)
//   node scripts/fetch-nasa-data.mjs --start 2024-05-01 --end 2024-05-31
//   node scripts/fetch-nasa-data.mjs --demo                  (usa la clave pública DEMO_KEY, con límites de uso)
//
// Salida: Assets/Game/Resources/Data/nasa_donki.json (lo lee el juego al arrancar).

import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const outFile = join(root, "Assets", "Game", "Resources", "Data", "nasa_donki.json");
const args = process.argv.slice(2);
const arg = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};

function readEnvLocal() {
  const file = join(root, ".env.local");
  if (!existsSync(file)) return undefined;
  for (const line of readFileSync(file, "utf8").split(/\r?\n/)) {
    const m = line.match(/^\s*NASA_API_KEY\s*=\s*(.+?)\s*$/);
    if (m) return m[1].replace(/^["']|["']$/g, "");
  }
  return undefined;
}

let apiKey = process.env.NASA_API_KEY || readEnvLocal();
if (args.includes("--demo")) apiKey = "DEMO_KEY";
if (!apiKey || apiKey === "YOUR_NASA_API_KEY") {
  console.error("Falta NASA_API_KEY. Copia .env.example a .env.local y pon tu clave, o usa --demo.");
  process.exit(1);
}

const isoDay = (d) => d.toISOString().slice(0, 10);
const end = arg("--end") ?? isoDay(new Date());
const start = arg("--start") ?? isoDay(new Date(Date.parse(end) - 60 * 86400000));

async function donki(type) {
  const url = new URL(`https://api.nasa.gov/DONKI/${type}`);
  url.searchParams.set("startDate", start);
  url.searchParams.set("endDate", end);
  url.searchParams.set("api_key", apiKey);
  const res = await fetch(url, { headers: { "User-Agent": "sol3-base-aurora-educational-game" } });
  if (!res.ok) throw new Error(`DONKI ${type}: HTTP ${res.status}`); // no se imprime la URL (contiene la clave)
  const text = await res.text();
  return text.trim() ? JSON.parse(text) : [];
}

const fmt = (t) => (t ? t.replace("T", " ").replace("Z", " UTC") : "fecha desconocida");

function flr(e) {
  return {
    type: "FLR",
    id: e.flrID ?? "",
    time: fmt(e.peakTime ?? e.beginTime),
    detail: `Fulguración clase ${e.classType ?? "?"}${e.sourceLocation ? ` en ${e.sourceLocation}` : ""}${e.activeRegionNum ? ` (región activa ${e.activeRegionNum})` : ""}`,
    link: e.link ?? "",
  };
}

function cme(e) {
  const analysis = (e.cmeAnalyses ?? []).find((a) => a.isMostAccurate) ?? (e.cmeAnalyses ?? [])[0];
  const speed = analysis?.speed ? `, velocidad estimada ${Math.round(analysis.speed)} km/s` : "";
  return {
    type: "CME",
    id: e.activityID ?? "",
    time: fmt(e.startTime),
    detail: `Eyección de masa coronal${e.sourceLocation ? ` desde ${e.sourceLocation}` : ""}${speed}`,
    link: e.link ?? "",
  };
}

function gst(e) {
  const kp = Math.max(0, ...(e.allKpIndex ?? []).map((k) => Number(k.kpIndex) || 0));
  return {
    type: "GST",
    id: e.gstID ?? "",
    time: fmt(e.startTime),
    detail: `Tormenta geomagnética en la Tierra, índice Kp máximo ${kp}`,
    link: e.link ?? "",
  };
}

try {
  const [f, c, g] = await Promise.all([donki("FLR"), donki("CME"), donki("GST")]);
  const records = [...f.map(flr), ...c.map(cme), ...g.map(gst)].sort((a, b) => (a.time < b.time ? 1 : -1));
  const data = {
    status: "ok",
    source: "NASA DONKI (api.nasa.gov) — Community Coordinated Modeling Center",
    queriedAt: new Date().toISOString().slice(0, 16).replace("T", " ") + " UTC",
    startDate: start,
    endDate: end,
    note: "Registros históricos; las tormentas geomagnéticas (GST) se miden en la Tierra, no en Marte.",
    records,
  };
  writeFileSync(outFile, JSON.stringify(data, null, 2) + "\n", "utf8");
  console.log(`Guardados ${records.length} registros (${f.length} FLR, ${c.length} CME, ${g.length} GST) de ${start} a ${end} en ${outFile}`);
} catch (err) {
  console.error("No se pudieron descargar los datos:", err.message);
  console.error("El archivo anterior se conserva; el juego funciona igual sin estos registros.");
  process.exit(2);
}
