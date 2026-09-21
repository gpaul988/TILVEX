"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { usePosStore } from "@/lib/store";
import type { BusinessType } from "@/lib/validation";

const types: { value: BusinessType; label: string; description: string }[] = [
  { value: "RETAIL", label: "Retail", description: "Shop, convenience, or general goods" },
  { value: "BEAUTY_SALON", label: "Beauty salon", description: "Salon, spa, or wellness studio" },
  { value: "BOUTIQUE", label: "Boutique", description: "Fashion, clothing, or accessories" },
  { value: "ELECTRICAL", label: "Electrical store", description: "Electrical supplies and equipment" },
  { value: "COMPUTER_REPAIR", label: "Computer repair", description: "Repairs, servicing, and support" },
  { value: "ELECTRONICS", label: "Electronics", description: "Consumer electronics and devices" },
  { value: "PHARMACY", label: "Pharmacy", description: "Medicines and health products" },
  { value: "GAS_PETROL_STATION", label: "Gas and petrol station", description: "Fuel, LPG, and forecourt sales" },
  { value: "GROCERY", label: "Grocery", description: "Food, household, and daily essentials" },
  { value: "HOSPITALITY", label: "Hospitality", description: "Cafe, restaurant, or bar" },
  { value: "SERVICE", label: "Service", description: "Appointments or service counter" },
  { value: "FUEL", label: "Fuel", description: "Forecourt or fuel station" },
  { value: "OTHER", label: "Other", description: "Something a little different" }
];

export function OnboardingForm({ authenticated }: { authenticated: boolean }) {
  const router = useRouter();
  const setBusiness = usePosStore((state) => state.setBusiness);
  const [businessType, setBusinessType] = useState<BusinessType>("RETAIL");
  const [values, setValues] = useState({ businessName: "", ownerName: "", email: "", password: "" });
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setSaving(true); setError("");
    const payload = authenticated
      ? { businessName: values.businessName, businessType }
      : { ...values, businessType };
    const response = await fetch("/api/auth/onboarding", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(payload) });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) { setError(result.error ?? "Unable to save setup."); setSaving(false); return; }
    setBusiness(values.businessName, businessType);
    router.push("/dashboard");
    router.refresh();
  }

  return <form onSubmit={submit} className="mt-10 space-y-8">
    <div><label className="text-sm font-semibold text-white" htmlFor="businessName">Business name</label><input id="businessName" required value={values.businessName} onChange={(event) => setValues({ ...values, businessName: event.target.value })} placeholder="e.g. TILVEX Central" className="mt-2 w-full rounded-lg border-line bg-panel text-sm text-white placeholder:text-slate-600" /></div>
    <div><p className="text-sm font-semibold text-white">What kind of business is this?</p><div className="mt-3 grid gap-2 sm:grid-cols-2">{types.map((type) => <button type="button" key={type.value} onClick={() => setBusinessType(type.value)} className={`rounded-xl border p-4 text-left transition ${businessType === type.value ? "border-accent bg-accent/10 shadow-halo" : "border-line bg-panel hover:border-accent/50"}`}><p className={`text-sm font-semibold ${businessType === type.value ? "text-accent" : "text-white"}`}>{type.label}</p><p className="mt-1 text-xs text-slate-500">{type.description}</p></button>)}</div></div>
    {!authenticated && <div className="grid gap-4 sm:grid-cols-2"><div><label className="text-sm font-semibold text-white" htmlFor="ownerName">Your name</label><input id="ownerName" required value={values.ownerName} onChange={(event) => setValues({ ...values, ownerName: event.target.value })} className="mt-2 w-full rounded-lg border-line bg-panel text-sm text-white" /></div><div><label className="text-sm font-semibold text-white" htmlFor="email">Work email</label><input id="email" required type="email" value={values.email} onChange={(event) => setValues({ ...values, email: event.target.value })} className="mt-2 w-full rounded-lg border-line bg-panel text-sm text-white" /></div><div className="sm:col-span-2"><label className="text-sm font-semibold text-white" htmlFor="password">Password</label><input id="password" required minLength={12} type="password" value={values.password} onChange={(event) => setValues({ ...values, password: event.target.value })} className="mt-2 w-full rounded-lg border-line bg-panel text-sm text-white" /><p className="mt-2 text-xs text-slate-500">Use at least 12 characters. It is stored as a one-way hash.</p></div></div>}
    {error && <p className="rounded-lg border border-accent/30 bg-accent/5 p-3 text-sm text-accent">{error}</p>}
    <button disabled={saving} className="rounded-lg bg-accent px-5 py-3 text-sm font-bold text-ink shadow-halo transition hover:bg-accent/90 disabled:cursor-wait disabled:opacity-60">{saving ? "Saving…" : "Open my command centre"} <span className="ml-2">→</span></button>
  </form>;
}
