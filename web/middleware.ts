import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { jwtVerify } from "jose";

const key = () => new TextEncoder().encode(process.env.AUTH_SECRET ?? "");

export async function middleware(request: NextRequest) {
  const token = request.cookies.get("greygas_session")?.value;
  if (!token || !process.env.AUTH_SECRET) {
    return NextResponse.redirect(new URL("/sign-in", request.url));
  }
  try {
    await jwtVerify(token, key());
    return NextResponse.next();
  } catch {
    return NextResponse.redirect(new URL("/sign-in", request.url));
  }
}

export const config = {
  matcher: ["/dashboard/:path*"]
};
