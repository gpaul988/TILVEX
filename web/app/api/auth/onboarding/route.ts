import bcrypt from "bcryptjs";
import { Prisma } from "@prisma/client";
import { NextResponse } from "next/server";
import { canAccess, createSession, getSession } from "@/lib/auth";
import { db } from "@/lib/db";
import { onboardingSchema, workspaceSchema } from "@/lib/validation";

export async function POST(request: Request) {
  const body = await request.json().catch(() => null);
  const session = await getSession();

  if (session) {
    if (!session.businessId || !canAccess(session.role, "ADMIN")) {
      return NextResponse.json({ error: "Only an administrator can update workspace settings." }, { status: 403 });
    }

    const parsed = workspaceSchema.safeParse(body);
    if (!parsed.success) {
      return NextResponse.json({
        error: "Check the business name and business type.",
        fields: parsed.error.flatten().fieldErrors
      }, { status: 400 });
    }

    const business = await db.$transaction(async (tx) => {
      const updated = await tx.business.update({
        where: { id: session.businessId! },
        data: { name: parsed.data.businessName, type: parsed.data.businessType }
      });
      for (const location of parsed.data.locations) {
        await tx.location.upsert({
          where: { id: `${session.businessId}-${location.name.toLowerCase().replace(/[^a-z0-9]+/g, "-")}` },
          update: { name: location.name, address: location.address },
          create: { id: `${session.businessId}-${location.name.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`, name: location.name, address: location.address, businessId: session.businessId! }
        });
      }
      return updated;
    });
    return NextResponse.json({ ok: true, businessId: business.id, updated: true });
  }

  const parsed = onboardingSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({
      error: "Complete all required account fields.",
      fields: parsed.error.flatten().fieldErrors
    }, { status: 400 });
  }

  const existing = await db.user.findUnique({ where: { email: parsed.data.email } });
  if (existing) return NextResponse.json({ error: "An account with this email already exists." }, { status: 409 });

  let business;
  try {
    business = await db.business.create({
      data: {
        workspaceKey: "PRIMARY",
        name: parsed.data.businessName,
        type: parsed.data.businessType,
        locations: { create: parsed.data.locations },
        programs: { create: [
          { name: "Service charge", kind: "SERVICE_CHARGE" },
          { name: "Surcharge", kind: "SURCHARGE" },
          { name: "Cash discount", kind: "CASH_DISCOUNT" },
          { name: "Dual pricing", kind: "DUAL_PRICING" },
          { name: "Tips", kind: "TIPS" }
        ] },
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
  } catch (error) {
    if (error instanceof Prisma.PrismaClientKnownRequestError && error.code === "P2002") {
      return NextResponse.json({
        error: "A workspace has already been created. Sign in to manage it instead of creating another workspace."
      }, { status: 409 });
    }
    throw error;
  }
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
