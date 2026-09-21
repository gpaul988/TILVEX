import type { Config } from "tailwindcss";
import forms from "@tailwindcss/forms";

const config: Config = {
  darkMode: "class",
  content: ["./app/**/*.{ts,tsx}", "./components/**/*.{ts,tsx}", "./lib/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: "#0d0f14",
        panel: "#141720",
        line: "#26303d",
        accent: "#00d4aa",
        "accent-dark": "#007a5e",
        mint: "#00a882"
      },
      boxShadow: {
        glow: "0 0 0 1px rgba(0,212,170,.16), 0 20px 70px rgba(0,0,0,.28)"
      }
    }
  },
  plugins: [forms]
};

export default config;
