import { NextResponse } from "next/server";
import { canAccess, getSession } from "@/lib/auth";
import { db } from "@/lib/db";
import { z } from "zod";

const operationSchema = z.object({
  kind: z.enum(["PROGRAM", "TAX", "ORDER", "TABLE", "EXCHANGE"]),
  name: z.string().trim().min(1).max(160),
  value: z.number().nonnegative().optional(),
  status: z.string().trim().max(40).optional(),
  locationId: z.string().optional(),
  reference: z.string().trim().max(100).optional(),
  reason: z.string().trim().max(240).optional()
});

export async function GET() {
  const session = await getSession();
  if (!session?.businessId) return NextResponse.json({ error: "Sign in required." }, { status: 401 });
  const [programs, taxes, orders, tables, exchanges] = await Promise.all([
    db.program.findMany({ where: { businessId: session.businessId } }),
    db.taxRule.findMany({ where: { businessId: session.businessId } }),
    db.order.findMany({ where: { businessId: session.businessId }, orderBy: { updatedAt: "desc" }, take: 100 }),
    db.restaurantTable.findMany({ where: { businessId: session.businessId }, orderBy: { name: "asc" } }),
    db.exchange.findMany({ where: { businessId: session.businessId }, orderBy: { createdAt: "desc" }, take: 100 })
  ]);
  return NextResponse.json({ programs, taxes, orders, tables, exchanges });
}

export async function POST(request: Request) {
  const session = await getSession();
  if (!session?.businessId || !canAccess(session.role, "MANAGER")) return NextResponse.json({ error: "Manager access required." }, { status: 403 });
  const parsed = operationSchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) return NextResponse.json({ error: "Invalid operational record." }, { status: 400 });
  const { kind, name, value = 0, status = "OPEN", locationId, reference, reason } = parsed.data;
  if (kind === "PROGRAM") return NextResponse.json({ record: await db.program.upsert({ where: { businessId_kind: { businessId: session.businessId, kind: name } }, update: { name, rate: value, enabled: true }, create: { name, kind: name, rate: value, businessId: session.businessId } }) }, { status: 201 });
  if (kind === "TAX") return NextResponse.json({ record: await db.taxRule.create({ data: { name, rate: value, businessId: session.businessId } }) }, { status: 201 });
  if (kind === "EXCHANGE") return NextResponse.json({ record: await db.exchange.create({ data: { originalRef: reference ?? name, reason: reason ?? "Exchange", amountDelta: value, businessId: session.businessId } }) }, { status: 201 });
  if (!locationId) return NextResponse.json({ error: "A location is required." }, { status: 400 });
  if (kind === "TABLE") return NextResponse.json({ record: await db.restaurantTable.create({ data: { name, status, locationId, businessId: session.businessId } }) }, { status: 201 });
  return NextResponse.json({ record: await db.order.create({ data: { number: name, status, locationId, businessId: session.businessId } }) }, { status: 201 });
}
