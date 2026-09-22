import bcrypt from "bcryptjs";
import { NextResponse } from "next/server";
import { canAccess, getSession } from "@/lib/auth";
import { db } from "@/lib/db";
import { roleSchema } from "@/lib/validation";
import { z } from "zod";

const userSchema = z.object({
  name: z.string().trim().min(2).max(100),
  email: z.string().trim().email().toLowerCase(),
  password: z.string().min(12).max(128),
  role: roleSchema
});

export async function GET() {
  const session = await getSession();
  if (!session?.businessId || !canAccess(session.role, "ADMIN")) return NextResponse.json({ error: "Administrator access required." }, { status: 403 });
  return NextResponse.json({ users: await db.user.findMany({ where: { businessId: session.businessId }, select: { id: true, name: true, email: true, role: true, lastLoginAt: true }, orderBy: { name: "asc" } }) });
}

export async function POST(request: Request) {
  const session = await getSession();
  if (!session?.businessId || session.role !== "SUPER_ADMIN") return NextResponse.json({ error: "Only a Super Admin can create users." }, { status: 403 });
  const parsed = userSchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) return NextResponse.json({ error: "Invalid user details.", fields: parsed.error.flatten().fieldErrors }, { status: 400 });
  const existing = await db.user.findUnique({ where: { email: parsed.data.email } });
  if (existing) return NextResponse.json({ error: "That email is already in use." }, { status: 409 });
  const user = await db.user.create({ data: { name: parsed.data.name, email: parsed.data.email, role: parsed.data.role, passwordHash: await bcrypt.hash(parsed.data.password, 12), businessId: session.businessId } });
  return NextResponse.json({ user: { id: user.id, name: user.name, email: user.email, role: user.role } }, { status: 201 });
}
