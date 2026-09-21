import { NextResponse } from "next/server";
import { db } from "@/lib/db";

export async function GET() {
  const checkedAt = new Date().toISOString();
  try {
    await db.$queryRaw`SELECT 1`;
    return NextResponse.json({ status: "ok", database: "ok", checkedAt });
  } catch {
    return NextResponse.json({ status: "degraded", database: "unavailable", checkedAt }, { status: 503 });
  }
}
