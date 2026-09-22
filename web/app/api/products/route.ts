import { NextResponse } from "next/server";
import { canAccess, getSession } from "@/lib/auth";
import { db } from "@/lib/db";
import { productSchema } from "@/lib/validation";

export async function GET() {
  const session = await getSession();
  if (!session?.businessId) return NextResponse.json({ error: "Sign in required." }, { status: 401 });
  return NextResponse.json({ products: await db.product.findMany({ where: { businessId: session.businessId, active: true }, orderBy: { name: "asc" } }) });
}

export async function POST(request: Request) {
  const session = await getSession();
  if (!session?.businessId || !canAccess(session.role, "MANAGER")) return NextResponse.json({ error: "Manager access required." }, { status: 403 });
  const parsed = productSchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) return NextResponse.json({ error: "Invalid product data.", fields: parsed.error.flatten().fieldErrors }, { status: 400 });
  const product = await db.product.create({ data: { ...parsed.data, businessId: session.businessId } });
  return NextResponse.json({ product }, { status: 201 });
}
