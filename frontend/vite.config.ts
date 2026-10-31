import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The build lands in the API's wwwroot, so one process serves both the SPA and /api.
export default defineConfig({
  plugins: [react()],
  build: { outDir: "../backend/FractionPlanner/wwwroot", emptyOutDir: true },
  server: { proxy: { "/api": "http://localhost:5080" } },
});
