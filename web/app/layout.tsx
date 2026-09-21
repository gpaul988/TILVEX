import type { Metadata, Viewport } from "next";
import Script from "next/script";
import "@/app/globals.css";

export const metadata: Metadata = {
  title: "TILVEX POS",
  description: "A calm command centre for modern point of sale operations",
  manifest: "/manifest.webmanifest",
  icons: {
    icon: "/tilvex-fav.svg",
    shortcut: "/tilvex-fav.svg",
    apple: "/tilvex-fav.svg"
  }
};

export const viewport: Viewport = {
  themeColor: "#141720",
  width: "device-width",
  initialScale: 1
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en" className="dark">
      <body>
        {children}
        <Script id="register-service-worker" strategy="afterInteractive">
          {`if ("serviceWorker" in navigator) window.addEventListener("load", () => navigator.serviceWorker.register("/sw.js"));`}
        </Script>
      </body>
    </html>
  );
}
