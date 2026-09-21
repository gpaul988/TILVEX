"use client";

import { create } from "zustand";
import type { BusinessType, Role } from "@/lib/validation";

type PosState = {
  businessName: string;
  businessType: BusinessType | null;
  role: Role;
  isOnline: boolean;
  setBusiness: (name: string, type: BusinessType) => void;
  setOnline: (online: boolean) => void;
};

export const usePosStore = create<PosState>((set) => ({
  businessName: "TILVEX Demo Store",
  businessType: "RETAIL",
  role: "MANAGER",
  isOnline: true,
  setBusiness: (businessName, businessType) => set({ businessName, businessType }),
  setOnline: (isOnline) => set({ isOnline })
}));
