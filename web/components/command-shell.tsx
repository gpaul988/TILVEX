"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { usePosStore } from "@/lib/store";
import { OfflineStatus } from "@/components/offline-status";
import type { Role } from "@/lib/validation";

const navigation: { href: string; label: string; icon: string; roles: Role[] }[] = [
  { href: "/dashboard", label: "Overview", icon: "⌂", roles: ["SUPER_ADMIN", "ADMIN", "MANAGER", "CASHIER", "VIEWER"] },
  { href: "/dashboard/sales", label: "Sales", icon: "↗", roles: ["SUPER_ADMIN", "ADMIN", "MANAGER", "CASHIER"] },
  { href: "/dashboard/inventory", label: "Inventory", icon: "▦", roles: ["SUPER_ADMIN", "ADMIN", "MANAGER"] },
  { href: "/dashboard/team", label: "Team", icon: "◎", roles: ["SUPER_ADMIN", "ADMIN", "MANAGER"] },
  { href: "/dashboard/settings", label: "Settings", icon: "⚙", roles: ["SUPER_ADMIN", "ADMIN"] }
] as const;

export function CommandShell({
  children,
  role = "MANAGER",
  userName = "Demo Operator"
}: {
  children: React.ReactNode;
  role?: Role;
  userName?: string;
}) {
  const pathname = usePathname();
  const businessName = usePosStore((state) => state.businessName);
  const visibleNavigation = navigation.filter((item) => item.roles.includes(role));

  return (
    <div className="min-h-screen bg-ink">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 flex-col border-r border-line bg-panel px-4 py-5 lg:flex">
        <Brand />
        <div className="mt-10 flex flex-1 flex-col">
          <p className="mb-3 px-3 text-[10px] font-bold uppercase tracking-[.22em] text-slate-500">Workspace</p>
          <nav className="space-y-1">
            {visibleNavigation.map((item) => (
              <NavItem key={item.href} {...item} active={pathname === item.href} />
            ))}
          </nav>
          <div className="mt-auto rounded-xl border border-line bg-ink/50 p-3">
            <p className="truncate text-sm font-semibold text-white">{businessName}</p>
            <p className="mt-1 text-xs text-slate-500">Phase 1 workspace</p>
            <div className="mt-3"><OfflineStatus /></div>
          </div>
        </div>
      </aside>

      <div className="lg:pl-64">
        <header className="sticky top-0 z-20 flex h-[76px] items-center justify-between border-b border-line bg-ink/90 px-5 backdrop-blur-md sm:px-8">
          <div className="flex items-center gap-3 lg:hidden"><Brand compact /></div>
          <div className="hidden text-sm text-slate-400 sm:block">Good morning, <span className="font-medium text-white">{userName}</span></div>
          <div className="ml-auto flex items-center gap-4">
            <OfflineStatus />
            <div className="flex h-9 w-9 items-center justify-center rounded-full bg-accent/15 text-sm font-bold text-accent">
              {userName.slice(0, 1).toUpperCase()}
            </div>
          </div>
        </header>
        <div className="flex min-h-[calc(100vh-76px)] flex-col">
          <main className="mx-auto w-full max-w-[1440px] flex-1 px-5 py-7 sm:px-8">{children}</main>
          <footer className="border-t border-line px-5 py-5 text-center sm:px-8">
            <p className="text-xs tracking-wide text-slate-500">
              <span className="font-semibold text-slate-300">TILVEX POS</span>
              <span className="mx-2 text-slate-600">by</span>
              <span className="font-semibold text-accent">Grey InfoTech</span>
            </p>
          </footer>
        </div>
      </div>

      <nav className="fixed inset-x-0 bottom-0 z-30 flex border-t border-line bg-panel/95 px-2 py-2 backdrop-blur lg:hidden">
        {visibleNavigation.slice(0, 4).map((item) => (
          <NavItem key={item.href} {...item} active={pathname === item.href} mobile />
        ))}
      </nav>
    </div>
  );
}

function Brand({ compact = false }: { compact?: boolean }) {
  return (
    <Link href="/dashboard" className="flex items-center gap-3">
      <img
        src={compact ? "/tilvex-icon-dark.svg" : "/tilvex-logo-horizontal-dark.svg"}
        alt="Tilvex POS"
        className={compact ? "h-9 w-9" : "h-10 w-auto"}
      />
    </Link>
  );
}

function NavItem({
  href,
  label,
  icon,
  active,
  mobile = false
}: {
  href: string;
  label: string;
  icon: string;
  active: boolean;
  mobile?: boolean;
}) {
  return (
    <Link href={href} className={`flex ${mobile ? "flex-1 flex-col justify-center gap-1 py-1 text-[10px]" : "gap-3 px-3 py-2.5 text-sm"} items-center rounded-lg transition ${active ? "bg-accent/10 font-semibold text-accent" : "text-slate-400 hover:bg-white/5 hover:text-white"}`}>
      <span className={mobile ? "text-base" : "w-5 text-center text-base"}>{icon}</span>
      <span>{label}</span>
    </Link>
  );
}
