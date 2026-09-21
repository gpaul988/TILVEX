"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";

export function SignInForm() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault(); setLoading(true); setError("");
    const response = await fetch("/api/auth/login", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ email, password }) });
    const result = await response.json();
    if (!response.ok) { setError(result.error ?? "Unable to sign in."); setLoading(false); return; }
    router.push("/dashboard"); router.refresh();
  }
  return <form onSubmit={submit} className="mt-8 space-y-5"><div><label htmlFor="email" className="text-sm font-semibold text-white">Email</label><input id="email" type="email" required autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} className="mt-2 w-full rounded-lg border-line bg-panel text-sm text-white" /></div><div><label htmlFor="password" className="text-sm font-semibold text-white">Password</label><input id="password" type="password" required autoComplete="current-password" value={password} onChange={(event) => setPassword(event.target.value)} className="mt-2 w-full rounded-lg border-line bg-panel text-sm text-white" /></div>{error && <p className="rounded-lg border border-accent/30 bg-accent/5 p-3 text-sm text-accent">{error}</p>}<button disabled={loading} className="w-full rounded-lg bg-accent px-5 py-3 text-sm font-bold text-ink shadow-halo hover:bg-accent/90 disabled:opacity-60">{loading ? "Signing in…" : "Sign in"}</button></form>;
}
