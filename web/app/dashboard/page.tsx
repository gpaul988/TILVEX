import { getSession } from "@/lib/auth";
import { MarginTrendKpi, PaymentDonutChart, RevenueLineChart, StockHealthGauge, TopProductsChart, TransactionBarsChart } from "@/components/dashboard-charts";

const stats = [
  { label: "Today’s sales", value: "£4,286.40", change: "+12.8%", detail: "vs. same day last week", tone: "text-mint" },
  { label: "Transactions", value: "184", change: "+8.2%", detail: "average basket £23.30", tone: "text-mint" },
  { label: "Low stock alerts", value: "7", change: "Needs attention", detail: "across 2 locations", tone: "text-accent" },
  { label: "Till status", value: "4 / 4", change: "All balanced", detail: "last close 21:34 yesterday", tone: "text-mint" }
];

const activity = [
  ["09:42", "Till 03 opened by Amara", "Operations"],
  ["09:18", "Stock count completed · Front shop", "Inventory"],
  ["08:55", "Morning shift scheduled", "Team"]
];

export default async function DashboardPage() {
  const session = await getSession();
  const role = session?.role ?? "MANAGER";
  return (
    <div className="space-y-8 pb-16 lg:pb-4">
      <section className="hud-grid futuristic-frame glass-panel scanline relative rounded-2xl p-5 sm:p-7 rise-in">
        <div>
          <p className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[.2em] text-accent"><span className="pulse-dot h-2 w-2 rounded-full bg-accent shadow-halo" />Command centre // live telemetry</p>
          <h1 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">Your operation, at a glance.</h1>
          <p className="mt-2 max-w-xl text-sm text-slate-400">A calm view of what needs your attention today. You are signed in as <span className="text-slate-200">{role.replace("_", " ")}</span>.</p>
        </div>
      </section>

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {stats.map((stat, index) => <StatCard key={stat.label} {...stat} delay={index} />)}
      </section>

      <section>
        <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[.2em] text-accent">Telemetry deck</p>
            <h2 className="mt-1 text-xl font-semibold text-white">Signals for the next move.</h2>
          </div>
          <p className="rounded-md border border-accent/20 bg-accent/5 px-3 py-1.5 text-[10px] uppercase tracking-[.14em] text-slate-400">Demo telemetry · Phase 1 has no sales feed</p>
        </div>
        <div className="grid gap-5 xl:grid-cols-2">
          <RevenueLineChart />
          <TransactionBarsChart />
          <PaymentDonutChart />
          <TopProductsChart />
          <StockHealthGauge />
          <MarginTrendKpi />
        </div>
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.5fr_1fr]">
        <div className="glass-panel hud-grid rounded-2xl p-5 sm:p-6">
          <div className="flex items-start justify-between">
            <div><h2 className="font-semibold text-white">Sales pulse</h2><p className="mt-1 text-xs text-slate-500">Last 7 days · all locations</p></div>
            <span className="rounded-md border border-accent/20 bg-accent/5 px-2 py-1 text-xs text-slate-400">This week⌄</span>
          </div>
          <div className="mt-8 flex h-48 items-end gap-2 sm:gap-4">
            {[42, 57, 48, 74, 61, 84, 68].map((height, index) => <div key={index} className="flex h-full flex-1 flex-col justify-end gap-2"><div className={`rounded-t-md ${index === 5 ? "bg-accent" : "bg-accent/25"}`} style={{ height: `${height}%` }} /><span className="text-center text-[10px] text-slate-500">{["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"][index]}</span></div>)}
          </div>
        </div>
        <div className="glass-panel rounded-2xl p-5 sm:p-6">
          <div className="flex items-start justify-between"><div><h2 className="font-semibold text-white">Recent activity</h2><p className="mt-1 text-xs text-slate-500">Today, 21 September</p></div><span className="text-xs text-mint">Live</span></div>
          <div className="mt-5 divide-y divide-line">
            {activity.map(([time, title, category]) => <div className="flex gap-3 py-4 first:pt-0 last:pb-0" key={title}><span className="pt-0.5 text-[11px] text-slate-500">{time}</span><div><p className="text-sm text-slate-200">{title}</p><p className="mt-1 text-xs text-slate-500">{category}</p></div></div>)}
          </div>
        </div>
      </section>

      <section className="glass-panel flex flex-col justify-between gap-4 rounded-2xl border-accent/25 bg-accent/5 p-5 sm:flex-row sm:items-center sm:p-6">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[.2em] text-accent">TILVEX Desktop</p>
          <h2 className="mt-2 font-semibold text-white">Run your POS locally</h2>
          <p className="mt-1 text-sm text-slate-400">Download the Windows desktop edition for local-first sales, stock, till, and reporting.</p>
        </div>
        <a href="/api/downloads/desktop" className="inline-flex w-fit items-center gap-2 rounded-lg bg-accent px-4 py-2.5 text-sm font-bold text-ink transition hover:bg-accent/90">
          Download desktop app <span>↓</span>
        </a>
      </section>
    </div>
  );
}

function StatCard({ label, value, change, detail, tone, delay }: { label: string; value: string; change: string; detail: string; tone: string; delay: number }) {
  return <div className="glass-panel hud-grid futuristic-frame rounded-2xl p-5 transition hover:border-accent/40 hover:shadow-halo rise-in" style={{ animationDelay: `${delay * 80}ms` }}><div className="flex items-center justify-between"><p className="text-xs font-medium uppercase tracking-wider text-slate-500">{label}</p><span className="pulse-dot h-1.5 w-1.5 rounded-full bg-accent shadow-[0_0_10px_rgba(0,212,170,.75)]" /></div><p className="mt-3 text-2xl font-semibold tracking-tight text-white">{value}</p><p className={`mt-2 text-xs font-semibold ${tone}`}>{change}</p><p className="mt-1 text-xs text-slate-500">{detail}</p></div>;
}
