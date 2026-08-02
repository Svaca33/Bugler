/**
 * Marks an episode the health check watch opened. It exists so the rail colour never has to carry
 * two meanings at once: red says trouble, this says the trouble is not in the logs — there is no
 * severity band to show and no log to open, because nothing was logged.
 */
export function HealthCheckBadge() {
  return (
    <span className="rounded bg-[rgba(140,161,184,0.15)] px-1.5 font-mono text-[10.5px] text-[#A9BDD1]">
      health check
    </span>
  );
}
