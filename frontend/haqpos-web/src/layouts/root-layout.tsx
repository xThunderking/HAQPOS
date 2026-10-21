import { Outlet } from "react-router";

export function RootLayout() {
  return (
    <main className="flex min-h-svh items-center justify-center p-6">
      <Outlet />
    </main>
  );
}
