import { getSession } from "@/lib/auth";
import { OnboardingForm } from "@/components/onboarding-form";

export default async function OnboardingPage() {
  const session = await getSession();
  return (
    <div className="flex min-h-screen flex-col bg-ink px-5 py-10 sm:px-8">
      <div className="mx-auto w-full max-w-2xl flex-1">
        <img src="/tilvex-logo-horizontal-dark.svg" alt="Tilvex POS" className="h-12 w-auto" />
        <div className="mt-16"><p className="text-xs font-bold uppercase tracking-[.2em] text-accent">{session ? "Workspace configuration" : "Welcome to TILVEX POS"}</p><h1 className="mt-3 text-4xl font-semibold tracking-tight text-white">Set up your command centre.</h1><p className="mt-3 max-w-lg text-slate-400">Start with the basics. You can add locations, products, and team members when those Phase 1 modules are ready.</p></div>
        <OnboardingForm authenticated={Boolean(session)} />
      </div>
      <footer className="pt-10 text-center text-xs tracking-wide text-slate-500">
        <span className="font-semibold text-slate-300">TILVEX POS</span>
        <span className="mx-2 text-slate-600">by</span>
        <span className="font-semibold text-accent">Grey InfoTech</span>
      </footer>
    </div>
  );
}
