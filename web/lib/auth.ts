import { SignJWT, jwtVerify } from "jose";
import { cookies } from "next/headers";
import { env } from "@/lib/config";
import { roleSchema, type Role } from "@/lib/validation";

const SESSION_COOKIE = "greygas_session";
const key = new TextEncoder().encode(env.AUTH_SECRET);

export type Session = {
  userId: string;
  email: string;
  name: string;
  role: Role;
  businessId: string | null;
};

export async function createSession(session: Session) {
  const token = await new SignJWT(session)
    .setProtectedHeader({ alg: "HS256", typ: "JWT" })
    .setIssuedAt()
    .setExpirationTime("8h")
    .sign(key);

  const cookieStore = await cookies();
  cookieStore.set(SESSION_COOKIE, token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge: 8 * 60 * 60
  });
}

export async function getSession(): Promise<Session | null> {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  if (!token) return null;
  try {
    const { payload } = await jwtVerify(token, key);
    if (
      typeof payload.userId !== "string" ||
      typeof payload.email !== "string" ||
      typeof payload.name !== "string"
    ) {
      return null;
    }
    const role = roleSchema.safeParse(payload.role);
    if (!role.success) return null;
    return {
      userId: payload.userId,
      email: payload.email,
      name: payload.name,
      role: role.data,
      businessId: typeof payload.businessId === "string" ? payload.businessId : null
    };
  } catch {
    return null;
  }
}

export async function clearSession() {
  (await cookies()).delete(SESSION_COOKIE);
}

export function canAccess(role: Role, minimum: Role) {
  const levels: Record<Role, number> = {
    VIEWER: 10,
    CASHIER: 20,
    MANAGER: 30,
    ADMIN: 40,
    SUPER_ADMIN: 50
  };
  return levels[role] >= levels[minimum];
}
