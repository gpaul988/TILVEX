import bcrypt from "bcryptjs";
import { NextResponse } from "next/server";
import { createSession } from "@/lib/auth";
import { db } from "@/lib/db";
import { onboardingSchema } from "@/lib/validation";

export async function POST(request: Request) {
  const parsed = onboardingSchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) return NextResponse.json({ error: "Check the highlighted setup fields." }, { status: 400 });

  const existing = await db.user.findUnique({ where: { email: parsed.data.email } });
  if (existing) return NextResponse.json({ error: "An account with this email already exists." }, { status: 409 });

  const business = await db.business.create({
    data: {
      name: parsed.data.businessName,
      type: parsed.data.businessType,
      users: {
        create: {
          name: parsed.data.ownerName,
          email: parsed.data.email,
          passwordHash: await bcrypt.hash(parsed.data.password, 12),
          role: "SUPER_ADMIN"
        }
      }
    },
    include: { users: true }
  });
  const owner = business.users[0];
  await createSession({
    userId: owner.id,
    email: owner.email,
    name: owner.name,
    role: owner.role,
    businessId: business.id
  });
  return NextResponse.json({ ok: true, businessId: business.id }, { status: 201 });
}
