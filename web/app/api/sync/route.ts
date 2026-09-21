import { NextResponse } from "next/server";
import { getSession } from "@/lib/auth";
import { db } from "@/lib/db";
import { syncEventSchema } from "@/lib/validation";

export async function POST(request: Request) {
  const session = await getSession();
  if (!session?.businessId) return NextResponse.json({ error: "A business session is required." }, { status: 401 });
  const parsed = syncEventSchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) return NextResponse.json({ error: "Invalid sync event." }, { status: 400 });

  await db.syncEvent.upsert({
    where: { id: parsed.data.id },
    update: { status: "RECEIVED", processedAt: new Date() },
    create: {
      id: parsed.data.id,
      entity: parsed.data.entity,
      operation: parsed.data.operation,
      payload: JSON.stringify(parsed.data.payload),
      status: "RECEIVED",
      processedAt: new Date(),
      businessId: session.businessId
    }
  });
  return NextResponse.json({ ok: true, id: parsed.data.id });
}
