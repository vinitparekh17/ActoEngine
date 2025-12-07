import { useEffect } from "react";
import { api } from "@/lib/api";

export function useCsrfInit() {
  useEffect(() => {
    const fetchCsrfToken = async () => {
      try {
        // This endpoint sets the XSRF-TOKEN cookie
        await api.get("/Csrf/token");
      } catch (error) {
        console.error("Failed to fetch CSRF token", error);
      }
    };

    fetchCsrfToken();
  }, []);
}
