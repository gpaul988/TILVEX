import bcrypt from "bcryptjs";
import { NextResponse } from "next/server";
import { createSession } from "@/lib/auth";
import { db } from "@/lib/db";
import { credentialsSchema } from "@/lib/validation";

export async function POST(request: Request) {
  const parsed = credentialsSchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) return NextResponse.json({ error: "Enter a valid email and password." }, { status: 400 });

  const user = await db.user.findUnique({ where: { email: parsed.data.email } });
  if (!user || !(await bcrypt.compare(parsed.data.password, user.passwordHash))) {
    return NextResponse.json({ error: "Email or password is incorrect." }, { status: 401 });
  }

  await db.user.update({ where: { id: user.id }, data: { lastLoginAt: new Date() } });
  await createSession({
    userId: user.id,
    email: user.email,
    name: user.name,
    role: user.role,
    businessId: user.businessId
  });
  return NextResponse.json({ ok: true, role: user.role });
}
