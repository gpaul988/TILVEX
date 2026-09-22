import Link from "next/link";
import { getSession } from "@/lib/auth";
import { db } from "@/lib/db";
import { SalesPos } from "@/components/sales-pos";

const sectionCopy: Record<string, { title: string; description: string }> = {
  sales: { title: "Sales workspace", description: "Sales capture, till sessions, and end-of-day reconciliation will live here." },
  inventory: { title: "Inventory workspace", description: "Stock levels, counts, and reorder signals will live here." },
  suppliers: { title: "Purchasing workspace", description: "Suppliers, purchase orders, and stock replenishment will live here." },
  team: { title: "Team workspace", description: "Roles, shifts, and staff access will live here." },
  settings: { title: "Workspace settings", description: "Business profile, locations, and integration configuration will live here." }
};

export default async function WorkspaceSection({ params }: { params: Promise<{ section: string }> }) {
  const { section } = await params;
  const content = sectionCopy[section] ?? { title: "Workspace", description: "This workspace is not available in Phase 1." };
  const session = await getSession();
  const products = session?.businessId && (section === "inventory" || section === "sales") ? await db.product.findMany({ where: { businessId: session.businessId, active: true }, orderBy: { name: "asc" }, take: 100 }) : [];
  const sales = session?.businessId && section === "sales" ? await db.sale.findMany({ where: { businessId: session.businessId }, orderBy: { createdAt: "desc" }, take: 12 }) : [];
  const suppliers = session?.businessId && section === "suppliers" ? await db.supplier.findMany({ where: { businessId: session.businessId }, orderBy: { name: "asc" }, take: 20 }) : [];
  return <div className="mx-auto max-w-5xl py-6 sm:py-12"><div className="glass-panel hud-grid rounded-2xl p-6 sm:p-8"><p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[.2em] text-accent"><span className="h-px w-8 bg-accent" />Live operations</p><h1 className="mt-3 text-4xl font-semibold text-white">{content.title}</h1><p className="mt-4 text-slate-400">{content.description}</p>
    {section === "inventory" && <DataTable title="Stock tracking" headers={["SKU", "Product", "Stock", "Reorder", "Price"]} rows={products.map((product) => [product.sku, product.name, `${product.stockQuantity} ${product.unit}`, `${product.reorderLevel}`, `£${product.price}`])} />}
    {section === "sales" && <><SalesPos products={products.map((product) => ({ id: product.id, sku: product.sku, name: product.name, price: Number(product.price), stockQuantity: Number(product.stockQuantity), unit: product.unit }))} /><DataTable title="Recent invoices and payments" headers={["Receipt", "Payment", "Subtotal", "Total", "Created"]} rows={sales.map((sale) => [sale.receiptNumber, sale.paymentMethod, `£${sale.subtotal}`, `£${sale.total}`, sale.createdAt.toLocaleString()])} /></>}
    {section === "suppliers" && <DataTable title="Suppliers" headers={["Supplier", "Contact", "Phone", "Email"]} rows={suppliers.map((supplier) => [supplier.name, supplier.contact ?? "—", supplier.phone ?? "—", supplier.email ?? "—"])} />}
    {!["inventory", "sales", "suppliers"].includes(section) && <div className="mt-8 rounded-2xl border border-dashed border-line bg-ink/60 p-6"><p className="text-sm leading-6 text-slate-300">This module is reserved for the next implementation slice. Existing desktop workflows remain available locally.</p></div>}
    <Link className="mt-8 inline-block text-sm font-semibold text-accent hover:underline" href="/dashboard">← Back to overview</Link></div></div>;
}

function DataTable({ title, headers, rows }: { title: string; headers: string[]; rows: string[][] }) {
  return <div className="mt-8 overflow-x-auto rounded-xl border border-line"><div className="border-b border-line px-4 py-3 text-sm font-semibold text-white">{title}</div><table className="w-full text-left text-sm"><thead className="bg-panel text-xs uppercase tracking-wide text-slate-500"><tr>{headers.map((header) => <th className="px-4 py-3" key={header}>{header}</th>)}</tr></thead><tbody className="divide-y divide-line">{rows.length ? rows.map((row, index) => <tr key={index} className="text-slate-300">{row.map((value, cell) => <td className="whitespace-nowrap px-4 py-3" key={cell}>{value}</td>)}</tr>) : <tr><td className="px-4 py-6 text-slate-500" colSpan={headers.length}>No records yet.</td></tr>}</tbody></table></div>;
}
