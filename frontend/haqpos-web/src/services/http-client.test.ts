import { afterEach, describe, expect, it, vi } from "vite-plus/test";
import { getJson } from "@/services/http-client";

afterEach(() => vi.unstubAllGlobals());

describe("Cliente HTTP técnico", () => {
  it("consulta la API a través de la ruta del proxy", async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ status: "ok" }));
    vi.stubGlobal("fetch", fetchMock);
    await expect(getJson("/health")).resolves.toEqual({ status: "ok" });
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/health",
      expect.objectContaining({
        headers: { Accept: "application/json" },
      }),
    );
  });
  it("propaga un fallo HTTP sin exponer el cuerpo de la respuesta", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response("internal details", { status: 503 })),
    );
    await expect(getJson("/health")).rejects.toThrow("La API respondió con HTTP 503.");
  });
});
