import { NextResponse } from "next/server";
import { canAccess, getSession } from "@/lib/auth";
import { db } from "@/lib/db";
import { saleSchema } from "@/lib/validation";

export async function GET() {
  const session = await getSession();
  if (!session?.businessId) return NextResponse.json({ error: "Sign in required." }, { status: 401 });
  return NextResponse.json({ sales: await db.sale.findMany({ where: { businessId: session.businessId }, include: { lines: true, payments: true, invoice: true }, orderBy: { createdAt: "desc" }, take: 100 }) });
}

export async function POST(request: Request) {
  const session = await getSession();
  if (!session?.businessId || !canAccess(session.role, "CASHIER")) return NextResponse.json({ error: "Cashier access required." }, { status: 403 });
  const parsed = saleSchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) return NextResponse.json({ error: "Invalid sale.", fields: parsed.error.flatten().fieldErrors }, { status: 400 });
  const quantities = new Map<string, number>();
  for (const line of parsed.data.lines) quantities.set(line.productId, (quantities.get(line.productId) ?? 0) + line.quantity);
  const productIds = [...quantities.keys()];
  const products = await db.product.findMany({ where: { businessId: session.businessId, id: { in: productIds }, active: true } });
  const byId = new Map(products.map((product) => [product.id, product]));
  for (const line of parsed.data.lines) {
    const product = byId.get(line.productId);
    if (!product || Number(product.stockQuantity) < (quantities.get(line.productId) ?? 0)) return NextResponse.json({ error: `Insufficient stock for ${product?.name ?? "product"}.` }, { status: 409 });
  }
  const subtotal = parsed.data.lines.reduce((sum, line) => sum + Number(byId.get(line.productId)!.price) * line.quantity, 0);
  const total = Math.max(0, subtotal - parsed.data.discount + parsed.data.tax);
  const paymentTotal = parsed.data.payments.reduce((sum, payment) => sum + payment.amount, 0);
  if (Math.abs(paymentTotal - total) > 0.005) return NextResponse.json({ error: `Payment allocations must equal the sale total (${total.toFixed(2)}).` }, { status: 400 });
  const paymentMethod = parsed.data.payments.length > 1 ? "SPLIT_PAYMENT" : parsed.data.payments[0].method;
  const sale = await db.$transaction(async (tx) => {
    const created = await tx.sale.create({
      data: {
        receiptNumber: parsed.data.receiptNumber, paymentMethod,
        customerName: parsed.data.customerName, subtotal, tax: parsed.data.tax, discount: parsed.data.discount, total,
        businessId: session.businessId!,
        lines: { create: parsed.data.lines.map((line) => ({ productId: line.productId, quantity: line.quantity, unitPrice: Number(byId.get(line.productId)!.price), name: byId.get(line.productId)!.name })) },
        payments: { create: parsed.data.payments },
        invoice: { create: { number: `INV-${parsed.data.receiptNumber}`, amount: total, paidAmount: total, businessId: session.businessId! } }
      }, include: { lines: true, payments: true, invoice: true }
    });
    for (const [productId, quantity] of quantities) await tx.product.update({ where: { id: productId }, data: { stockQuantity: { decrement: quantity } } });
    return created;
  });
  return NextResponse.json({ sale }, { status: 201 });
}
