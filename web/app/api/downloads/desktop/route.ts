import { promises as fs } from "node:fs";
import path from "node:path";
import { NextResponse } from "next/server";
import { getSession } from "@/lib/auth";

const downloadName = "TILVEX-desktop.zip";

export async function GET() {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: "Sign in to download TILVEX Desktop." }, { status: 401 });

  const configuredPath = process.env.TILVEX_DESKTOP_DOWNLOAD_PATH;
  const archivePath = configuredPath
    ? path.resolve(configuredPath)
    : path.join(process.cwd(), "private-downloads", downloadName);

  let archive: Buffer;
  try {
    archive = await fs.readFile(archivePath);
  } catch {
    return NextResponse.json({ error: "The TILVEX Desktop download is not available yet." }, { status: 503 });
  }

  return new NextResponse(archive as BodyInit, {
    headers: {
      "content-type": "application/zip",
      "content-disposition": `attachment; filename="${downloadName}"`,
      "cache-control": "private, no-store"
    }
  });
}
