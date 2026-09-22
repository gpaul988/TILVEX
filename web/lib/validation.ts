import { z } from "zod";

export const roleSchema = z.enum(["SUPER_ADMIN", "ADMIN", "MANAGER", "CASHIER", "VIEWER"]);
export type Role = z.infer<typeof roleSchema>;

export const businessTypeSchema = z.enum([
  "RETAIL",
  "BEAUTY_SALON",
  "BOUTIQUE",
  "ELECTRICAL",
  "COMPUTER_REPAIR",
  "ELECTRONICS",
  "PHARMACY",
  "GAS_PETROL_STATION",
  "GROCERY",
  "HOSPITALITY",
  "SERVICE",
  "FUEL",
  "OTHER"
]);
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
  password: z.string().min(12).max(128),
  locations: z.array(z.object({
    name: z.string().trim().min(2).max(100),
    address: z.string().trim().max(240).optional()
  })).min(1).max(100)
});

export const profileSyncSchema = onboardingSchema.extend({
  locations: onboardingSchema.shape.locations.default([{ name: "Main location" }]),
  password: z.string().min(8).max(128),
  mode: z.enum(["REGISTER", "LOGIN"])
});

export const workspaceSchema = z.object({
  businessName: z.string().trim().min(2, "Business name must be at least 2 characters.").max(100),
  businessType: businessTypeSchema,
  locations: z.array(z.object({
    name: z.string().trim().min(2).max(100),
    address: z.string().trim().max(240).optional()
  })).min(1).max(100)
});

export const syncEventSchema = z.object({
  id: z.string().uuid(),
  entity: z.string().trim().min(1).max(80),
  operation: z.enum(["CREATE", "UPDATE", "DELETE"]),
  payload: z.record(z.string(), z.unknown()),
  createdAt: z.string().datetime(),
  attempts: z.number().int().nonnegative()
});

export const productSchema = z.object({
  sku: z.string().trim().min(1).max(80),
  barcode: z.string().trim().max(80).optional(),
  name: z.string().trim().min(1).max(160),
  category: z.string().trim().max(100).default(""),
  unit: z.string().trim().min(1).max(30).default("each"),
  price: z.number().nonnegative(),
  costPrice: z.number().nonnegative().default(0),
  stockQuantity: z.number().nonnegative().default(0),
  reorderLevel: z.number().nonnegative().default(0)
});

export const saleSchema = z.object({
  receiptNumber: z.string().trim().min(1).max(80),
  customerName: z.string().trim().max(160).optional(),
  tax: z.number().nonnegative().default(0),
  discount: z.number().nonnegative().default(0),
  lines: z.array(z.object({
    productId: z.string().min(1),
    quantity: z.number().positive(),
    unitPrice: z.number().nonnegative()
  })).min(1),
  payments: z.array(z.object({
    method: z.enum(["LOYALTY_POINTS", "GIFT_CARD", "ON_ACCOUNT", "STORE_CREDIT", "BANK_TRANSFER", "CASH"]),
    amount: z.number().positive(),
    reference: z.string().trim().max(120).optional()
  })).min(1)
});
