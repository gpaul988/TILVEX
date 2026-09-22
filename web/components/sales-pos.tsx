"use client";

import { useMemo, useState } from "react";

type Product = {
  id: string;
  sku: string;
  name: string;
  price: number;
  stockQuantity: number;
  unit: string;
};

type CartLine = Product & { quantity: number };

const paymentMethods = [
  ["CASH", "Cash"],
  ["BANK_TRANSFER", "Bank transfer"],
  ["GIFT_CARD", "Gift card"],
  ["LOYALTY_POINTS", "Loyalty points"],
  ["ON_ACCOUNT", "On account"],
  ["STORE_CREDIT", "Store credit"],
  ["SPLIT_PAYMENT", "Split payment"]
] as const;

export function SalesPos({ products: initialProducts }: { products: Product[] }) {
  const [products, setProducts] = useState(initialProducts);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [search, setSearch] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("CASH");
  const [tendered, setTendered] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [splitPayments, setSplitPayments] = useState("CASH:0");
  const [message, setMessage] = useState("");
  const [saving, setSaving] = useState(false);

  const visibleProducts = products.filter((product) =>
    `${product.name} ${product.sku}`.toLowerCase().includes(search.toLowerCase())
  );
  const total = useMemo(() => cart.reduce((sum, line) => sum + line.price * line.quantity, 0), [cart]);

  function addProduct(product: Product) {
    setMessage("");
    setCart((current) => {
      const existing = current.find((line) => line.id === product.id);
      if (existing) {
        if (existing.quantity >= product.stockQuantity) return current;
        return current.map((line) => line.id === product.id ? { ...line, quantity: line.quantity + 1 } : line);
      }
      return [...current, { ...product, quantity: 1 }];
    });
  }

  function updateQuantity(id: string, quantity: number) {
    setCart((current) => current.flatMap((line) => line.id === id
      ? quantity > 0 && quantity <= line.stockQuantity ? [{ ...line, quantity }] : quantity <= 0 ? [] : [line]
      : [line]));
  }

  async function completeSale() {
    if (!cart.length) return setMessage("Add at least one product before completing the sale.");
    const payments = paymentMethod === "SPLIT_PAYMENT"
      ? splitPayments.split(",").map((entry) => {
          const [method, amount] = entry.split(":");
          return { method: method.trim().toUpperCase(), amount: Number(amount) };
        }).filter((payment) => payment.method && Number.isFinite(payment.amount) && payment.amount > 0)
      : [{ method: paymentMethod, amount: Number(tendered) }];
    if (!payments.length || Math.abs(payments.reduce((sum, payment) => sum + payment.amount, 0) - total) > 0.005) {
      return setMessage(`Payment allocations must equal £${total.toFixed(2)}.`);
    }
    setSaving(true);
    setMessage("");
    try {
      const response = await fetch("/api/sales", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          receiptNumber: `WEB-${Date.now()}`,
          customerName: customerName.trim() || undefined,
          lines: cart.map((line) => ({ productId: line.id, quantity: line.quantity, unitPrice: line.price })),
          payments
        })
      });
      const result = await response.json();
      if (!response.ok) throw new Error(result.error ?? "The sale could not be completed.");
      setProducts((current) => current.map((product) => {
        const sold = cart.find((line) => line.id === product.id)?.quantity ?? 0;
        return { ...product, stockQuantity: product.stockQuantity - sold };
      }));
      setCart([]);
      setTendered("");
      setCustomerName("");
      setMessage(`Sale ${result.sale.receiptNumber} completed.`);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "The sale could not be completed.");
    } finally {
      setSaving(false);
    }
  }

  return <div className="mt-8 grid gap-5 xl:grid-cols-[1.15fr_.85fr]">
    <section className="rounded-xl border border-line bg-ink/50 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div><p className="text-xs font-semibold uppercase tracking-[.18em] text-accent">Live POS</p><h2 className="mt-1 text-lg font-semibold text-white">Build a sale</h2></div>
        <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search SKU or product" className="w-full rounded-lg border border-line bg-panel px-3 py-2 text-sm text-white outline-none focus:border-accent sm:w-56" />
      </div>
      <div className="mt-4 grid max-h-[440px] gap-2 overflow-y-auto">
        {visibleProducts.map((product) => <button key={product.id} disabled={product.stockQuantity <= 0} onClick={() => addProduct(product)} className="flex items-center justify-between rounded-lg border border-line bg-panel/70 px-3 py-3 text-left transition hover:border-accent/60 disabled:cursor-not-allowed disabled:opacity-40">
          <span><span className="block text-sm font-semibold text-white">{product.name}</span><span className="text-xs text-slate-500">{product.sku} · {product.stockQuantity} {product.unit} available</span></span><span className="text-sm font-bold text-accent">£{product.price.toFixed(2)}</span>
        </button>)}
        {!visibleProducts.length && <p className="py-8 text-center text-sm text-slate-500">No matching products.</p>}
      </div>
    </section>
    <section className="rounded-xl border border-accent/25 bg-accent/5 p-4">
      <div className="flex items-center justify-between"><h2 className="text-lg font-semibold text-white">Current sale</h2><span className="text-xs uppercase tracking-wider text-slate-500">{cart.length} lines</span></div>
      <div className="mt-4 space-y-2">
        {cart.map((line) => <div key={line.id} className="flex items-center justify-between gap-2 rounded-lg border border-line bg-panel/70 p-3"><span className="min-w-0 flex-1 truncate text-sm text-slate-200">{line.name}<span className="ml-2 text-xs text-slate-500">£{line.price.toFixed(2)}</span></span><input type="number" min="1" max={line.stockQuantity} value={line.quantity} onChange={(event) => updateQuantity(line.id, Number(event.target.value))} className="w-16 rounded border border-line bg-ink px-2 py-1 text-center text-sm text-white" /><span className="w-20 text-right text-sm font-semibold text-white">£{(line.price * line.quantity).toFixed(2)}</span></div>)}
        {!cart.length && <p className="rounded-lg border border-dashed border-line py-8 text-center text-sm text-slate-500">Select products to start.</p>}
      </div>
      <div className="mt-4 space-y-3 border-t border-line pt-4">
        <input value={customerName} onChange={(event) => setCustomerName(event.target.value)} placeholder="Customer name (optional)" className="w-full rounded-lg border border-line bg-panel px-3 py-2 text-sm text-white outline-none focus:border-accent" />
        <select value={paymentMethod} onChange={(event) => setPaymentMethod(event.target.value)} className="w-full rounded-lg border border-line bg-panel px-3 py-2 text-sm text-white outline-none focus:border-accent">{paymentMethods.map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select>
        {paymentMethod === "SPLIT_PAYMENT" ? <input value={splitPayments} onChange={(event) => setSplitPayments(event.target.value)} placeholder="CASH:10,GIFT_CARD:5" className="w-full rounded-lg border border-line bg-panel px-3 py-2 text-sm text-white outline-none focus:border-accent" /> : <input type="number" min="0" step="0.01" value={tendered} onChange={(event) => setTendered(event.target.value)} placeholder={`Amount received (£${total.toFixed(2)})`} className="w-full rounded-lg border border-line bg-panel px-3 py-2 text-sm text-white outline-none focus:border-accent" />}
        <div className="flex items-end justify-between"><span className="text-sm text-slate-400">Total</span><span className="text-2xl font-bold text-accent">£{total.toFixed(2)}</span></div>
        {message && <p className="rounded-lg border border-accent/20 bg-accent/10 p-2 text-sm text-slate-200">{message}</p>}
        <button disabled={saving || !cart.length} onClick={completeSale} className="w-full rounded-lg bg-accent px-4 py-3 text-sm font-bold text-ink transition hover:bg-accent/90 disabled:cursor-not-allowed disabled:opacity-50">{saving ? "Processing..." : "Complete sale"}</button>
      </div>
    </section>
  </div>;
}
