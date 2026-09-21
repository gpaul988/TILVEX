"use client";

import { useEffect } from "react";
import { usePosStore } from "@/lib/store";
import { flushQueue } from "@/lib/offline-queue";

export function OfflineStatus() {
  const online = usePosStore((state) => state.isOnline);
  const setOnline = usePosStore((state) => state.setOnline);

  useEffect(() => {
    const update = () => {
      setOnline(navigator.onLine);
      if (navigator.onLine) void flushQueue();
    };
    update();
    window.addEventListener("online", update);
    window.addEventListener("offline", update);
    return () => {
      window.removeEventListener("online", update);
      window.removeEventListener("offline", update);
    };
  }, [setOnline]);

  return (
    <span className={`inline-flex items-center gap-2 text-xs font-medium ${online ? "text-mint" : "text-accent"}`}>
      <span className={`h-2 w-2 rounded-full ${online ? "bg-mint" : "bg-accent"}`} />
      {online ? "Online" : "Offline mode"}
    </span>
  );
}
