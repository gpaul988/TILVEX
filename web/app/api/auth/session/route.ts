import { NextResponse } from "next/server";
import { clearSession, getSession } from "@/lib/auth";

export async function GET() {
  return NextResponse.json({ session: await getSession() });
}

export async function DELETE() {
  await clearSession();
  return NextResponse.json({ ok: true });
}
