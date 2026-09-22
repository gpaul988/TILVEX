import bcrypt from "bcryptjs";
import { NextResponse } from "next/server";
import { createSession } from "@/lib/auth";
import { db } from "@/lib/db";
import { profileSyncSchema } from "@/lib/validation";

export async function POST(request: Request) {
  const parsed = profileSyncSchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) return NextResponse.json({ error: "The shared profile details are invalid." }, { status: 400 });

  const { mode, email, password, ownerName, businessName, businessType } = parsed.data;
  const existingUser = await db.user.findUnique({ where: { email }, include: { business: true } });

  if (existingUser) {
    if (!(await bcrypt.compare(password, existingUser.passwordHash))) {
      return NextResponse.json({ error: "A different password is already registered for this profile." }, { status: 409 });
    }
    if (mode === "REGISTER") return NextResponse.json({ error: "This profile already exists. Sign in instead." }, { status: 409 });
    await db.user.update({ where: { id: existingUser.id }, data: { lastLoginAt: new Date() } });
    await createSession({
      userId: existingUser.id,
      email: existingUser.email,
      name: existingUser.name,
      role: existingUser.role,
      businessId: existingUser.businessId
    });
    return NextResponse.json({
      ok: true,
      businessId: existingUser.businessId,
      role: existingUser.role,
      name: existingUser.name,
      businessName: existingUser.business?.name,
      businessType: existingUser.business?.type
    });
  }

  if (mode === "LOGIN") return NextResponse.json({ error: "Create this profile first from the desktop or web app." }, { status: 404 });
  if (await db.business.findFirst({ where: { workspaceKey: "PRIMARY" } })) {
    return NextResponse.json({ error: "A workspace already exists. Use its existing owner profile." }, { status: 409 });
  }

  const business = await db.business.create({
    data: {
      workspaceKey: "PRIMARY",
      name: businessName,
      type: businessType,
      users: {
        create: {
          name: ownerName,
          email,
          passwordHash: await bcrypt.hash(password, 12),
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
  return NextResponse.json({ ok: true, businessId: business.id, role: owner.role }, { status: 201 });
}
