import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react-swc";
import { defineConfig } from "vite";
import { resolveBackendOrigin } from "./src/config/backend";

const backendOrigin = resolveBackendOrigin(process.env.VITE_API_BASE_URL);

// https://vite.dev/config/
export default defineConfig({
  base: "/",
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    proxy: {
      "/api": {
        target: backendOrigin,
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
