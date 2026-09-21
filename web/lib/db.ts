import { PrismaClient } from "@prisma/client";

const globalForPrisma = globalThis as unknown as { prisma?: PrismaClient };

function getPrisma() {
  if (!globalForPrisma.prisma) {
    globalForPrisma.prisma = new PrismaClient({
      log: process.env.NODE_ENV === "development" ? ["error", "warn"] : ["error"]
    });
  }
  return globalForPrisma.prisma;
}

// Keep route modules importable during `next build`; Prisma is initialised only
// when an API handler actually needs the database.
export const db = new Proxy({} as PrismaClient, {
  get(_target, property) {
    const client = getPrisma() as unknown as Record<PropertyKey, unknown>;
    return client[property];
  }
});
