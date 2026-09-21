import { z } from "zod";

export const roleSchema = z.enum(["SUPER_ADMIN", "ADMIN", "MANAGER", "CASHIER", "VIEWER"]);
export type Role = z.infer<typeof roleSchema>;

export const businessTypeSchema = z.enum(["RETAIL", "HOSPITALITY", "SERVICE", "FUEL", "OTHER"]);
export type BusinessType = z.infer<typeof businessTypeSchema>;

export const credentialsSchema = z.object({
  email: z.string().trim().email().toLowerCase(),
  password: z.string().min(8).max(128)
});

export const onboardingSchema = z.object({
  businessName: z.string().trim().min(2).max(100),
  businessType: businessTypeSchema,
  ownerName: z.string().trim().min(2).max(100),
  email: z.string().trim().email().toLowerCase(),
  password: z.string().min(12).max(128)
});

export const syncEventSchema = z.object({
  id: z.string().uuid(),
  entity: z.string().trim().min(1).max(80),
  operation: z.enum(["CREATE", "UPDATE", "DELETE"]),
  payload: z.record(z.string(), z.unknown()),
  createdAt: z.string().datetime(),
  attempts: z.number().int().nonnegative()
});
