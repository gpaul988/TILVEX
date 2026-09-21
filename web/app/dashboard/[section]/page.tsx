import Link from "next/link";

const sectionCopy: Record<string, { title: string; description: string }> = {
  sales: { title: "Sales workspace", description: "Sales capture, till sessions, and end-of-day reconciliation will live here." },
  inventory: { title: "Inventory workspace", description: "Stock levels, counts, and reorder signals will live here." },
  team: { title: "Team workspace", description: "Roles, shifts, and staff access will live here." },
  settings: { title: "Workspace settings", description: "Business profile, locations, and integration configuration will live here." }
};

export default async function WorkspaceSection({ params }: { params: Promise<{ section: string }> }) {
  const { section } = await params;
  const content = sectionCopy[section] ?? { title: "Workspace", description: "This workspace is not available in Phase 1." };
  return <div className="mx-auto max-w-3xl py-12"><p className="text-xs font-semibold uppercase tracking-[.2em] text-accent">Phase 1 foundation</p><h1 className="mt-3 text-4xl font-semibold text-white">{content.title}</h1><p className="mt-4 text-slate-400">{content.description}</p><div className="mt-8 rounded-2xl border border-dashed border-line bg-panel/60 p-6"><p className="text-sm leading-6 text-slate-300">The command centre is ready for this module. No external integration is implied or active yet; future adapters will be added behind the authenticated API and offline sync queue.</p></div><Link className="mt-8 inline-block text-sm font-semibold text-accent hover:underline" href="/dashboard">← Back to overview</Link></div>;
}
