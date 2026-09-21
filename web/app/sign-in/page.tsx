import Link from "next/link";
import { SignInForm } from "@/components/sign-in-form";

export default function SignInPage() {
  return <main className="flex min-h-screen flex-col bg-ink px-5 py-10"><div className="flex flex-1 items-center justify-center"><div className="w-full max-w-md"><img src="/tilvex-logo-horizontal-dark.svg" alt="Tilvex POS" className="h-12 w-auto" /><div className="mt-14"><p className="text-xs font-bold uppercase tracking-[.2em] text-accent">Welcome back</p><h1 className="mt-3 text-3xl font-semibold text-white">Sign in to your workspace.</h1><p className="mt-2 text-sm text-slate-400">Credentials are local to this Phase 1 environment.</p></div><SignInForm /><p className="mt-8 text-center text-sm text-slate-500">New workspace? <Link href="/onboarding" className="font-semibold text-accent hover:underline">Start onboarding</Link></p></div></div><footer className="pt-10 text-center text-xs tracking-wide text-slate-500"><span className="font-semibold text-slate-300">TILVEX POS</span><span className="mx-2 text-slate-600">by</span><span className="font-semibold text-accent">Grey InfoTech</span></footer></main>;
}
