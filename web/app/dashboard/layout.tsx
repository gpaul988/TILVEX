import { getSession } from "@/lib/auth";
import { CommandShell } from "@/components/command-shell";

export default async function DashboardLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const session = await getSession();
  return <CommandShell role={session?.role} userName={session?.name}>{children}</CommandShell>;
}
