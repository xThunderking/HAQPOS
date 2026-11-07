import { useEffect, useRef, useState } from "react";

export function useDebouncedSearch(value: string) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [value]);
  return debounced;
}

// Retain failed requests so retrying an action cannot consume stock twice.
export function useCashCommandId() {
  const pending = useRef<{ key: string; id: string } | null>(null);
  return {
    for: (key: string) => {
      const storageKey = `haqpos.cash.${key}`;
      try {
        const stored = sessionStorage.getItem(storageKey);
        pending.current = { key: storageKey, id: stored || crypto.randomUUID() };
        sessionStorage.setItem(storageKey, pending.current.id);
      } catch {
        if (pending.current?.key !== storageKey)
          pending.current = { key: storageKey, id: crypto.randomUUID() };
      }
      return pending.current.id;
    },
    clear: () => {
      try {
        if (pending.current) sessionStorage.removeItem(pending.current.key);
      } catch {
        /* Storage can be disabled. */
      }
      pending.current = null;
    },
  };
}

export function resolveCashCommands(ids: readonly string[]) {
  try {
    for (const key of Object.keys(sessionStorage)) {
      if (key.startsWith("haqpos.cash.") && ids.includes(sessionStorage.getItem(key) ?? ""))
        sessionStorage.removeItem(key);
    }
  } catch {
    /* The command also works without persistent browser storage. */
  }
}
