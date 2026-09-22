import { NextResponse } from "next/server";
import { canAccess, getSession } from "@/lib/auth";
import { db } from "@/lib/db";
import { z } from "zod";

const supplierSchema = z.object({ name: z.string().trim().min(1).max(160), contact: z.string().trim().max(160).optional(), email: z.string().email().optional(), phone: z.string().trim().max(40).optional(), address: z.string().trim().max(240).optional() });
export async function GET() {
  const session = await getSession();
  if (!session?.businessId) return NextResponse.json({ error: "Sign in required." }, { status: 401 });
  return NextResponse.json({ suppliers: await db.supplier.findMany({ where: { businessId: session.businessId }, orderBy: { name: "asc" } }) });
}
export async function POST(request: Request) {
  const session = await getSession();
  if (!session?.businessId || !canAccess(session.role, "MANAGER")) return NextResponse.json({ error: "Manager access required." }, { status: 403 });
  const parsed = supplierSchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) return NextResponse.json({ error: "Invalid supplier." }, { status: 400 });
  return NextResponse.json({ supplier: await db.supplier.create({ data: { ...parsed.data, businessId: session.businessId } }) }, { status: 201 });
}
